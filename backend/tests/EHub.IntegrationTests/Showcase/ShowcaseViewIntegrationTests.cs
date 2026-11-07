using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EHub.IntegrationTests.Showcase;

[Collection("Sequential")]
public sealed class ShowcaseViewIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public Task RecordsOneViewPerViewerAndPageUntilUtcMidnight_AndIgnoresOwners() =>
        WithShowcasesAsync(async scenario =>
        {
            var firstId = scenario.First.Id;
            (await scenario.ViewAsync(firstId, scenario.Owner)).Should().Be(4);
            (await scenario.ViewAsync(firstId, scenario.Viewer)).Should().Be(5);
            (await scenario.ViewAsync(firstId, scenario.Viewer)).Should().Be(5);
            (await scenario.ViewAsync(firstId, scenario.OtherViewer)).Should().Be(6);
            (await scenario.ViewAsync(scenario.Second.Id, scenario.Viewer)).Should().Be(1);

            scenario.Clock.UtcNow = scenario.Clock.UtcNow.AddMinutes(2);
            (await scenario.ViewAsync(firstId, scenario.Viewer)).Should().Be(7);
            (await scenario.ViewAsync(firstId, scenario.Viewer)).Should().Be(7);
            (await scenario.ViewAsync(firstId, scenario.Owner)).Should().Be(7);

            var records = await scenario.Db.ShowcaseViewsDaily.AsNoTracking()
                .Where(view => view.ShowcaseProjectId == firstId)
                .ToListAsync();
            records.Should().HaveCount(3).And.OnlyContain(view => view.UserId != scenario.Owner.Id);
            records.Where(view => view.UserId == scenario.Viewer.Id).Select(view => view.ViewDateUtc)
                .Should().BeEquivalentTo(new[] { new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 7) });
            using var detailResponse = await scenario.SendAsync(HttpMethod.Get, $"/api/showcase/{firstId}", scenario.Viewer);
            detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var detail = (await detailResponse.Content.ReadFromJsonAsync<ApiResponse<ShowcaseDetailDto>>())!.Data!;
            detail.Showcase.ViewCount.Should().Be(7);
        });

    [Fact]
    public Task ConcurrentRequests_CountDistinctViewersExactlyOnce() =>
        WithShowcasesAsync(async scenario =>
        {
            var id = scenario.First.Id;
            var viewers = Enumerable.Repeat(scenario.Viewer, 4).Concat(Enumerable.Repeat(scenario.OtherViewer, 4));
            var counts = await Task.WhenAll(viewers.Select(user => scenario.ViewAsync(id, user)));
            counts.Should().OnlyContain(count => count >= 5 && count <= 6);
            (await scenario.Db.ShowcaseProjects.Where(page => page.Id == id).Select(page => page.ViewCount).SingleAsync())
                .Should().Be(6);
            (await scenario.Db.ShowcaseViewsDaily.CountAsync(view => view.ShowcaseProjectId == id)).Should().Be(2);
        });

    [Fact]
    public Task MissingHiddenAndSoftDeletedPagesReturnNotFoundAndKeepTheirCounters() =>
        WithShowcasesAsync(async scenario =>
        {
            var id = scenario.First.Id;
            foreach (var status in new[] { ShowcaseStatus.Draft, ShowcaseStatus.PendingReview, ShowcaseStatus.Hidden })
            {
                await scenario.Db.ShowcaseProjects.Where(page => page.Id == id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(page => page.Status, status));
                using var unpublished = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{id}/view", scenario.Viewer);
                unpublished.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
            using var ownerOnHidden = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{id}/view", scenario.Owner);
            ownerOnHidden.StatusCode.Should().Be(HttpStatusCode.NotFound);

            await scenario.Db.ShowcaseProjects.Where(page => page.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(page => page.Status, ShowcaseStatus.Published)
                    .SetProperty(page => page.IsDeleted, true));
            using var deleted = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{id}/view", scenario.Viewer);
            deleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
            using var missing = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{Guid.NewGuid()}/view", scenario.Viewer);
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await scenario.Db.ShowcaseViewsDaily.AnyAsync(view => view.ShowcaseProjectId == id)).Should().BeFalse();
            (await scenario.Db.ShowcaseProjects.IgnoreQueryFilters().Where(page => page.Id == id).Select(page => page.ViewCount).SingleAsync())
                .Should().Be(4);
        });

    [Fact]
    public Task Trending_UsesViewsFromTodayAndExpiresTheirBonusAtUtcMidnight() =>
        WithShowcasesAsync(async scenario =>
        {
            scenario.First.PublishedAt = scenario.Clock.UtcNow.AddDays(-7);
            scenario.Second.PublishedAt = scenario.Clock.UtcNow.AddDays(-6);
            await scenario.Db.SaveChangesAsync();

            (await FirstRankedAsync()).Should().Be(scenario.Second.Id);
            await scenario.ViewAsync(scenario.First.Id, scenario.Viewer);
            (await FirstRankedAsync()).Should().Be(scenario.First.Id);

            scenario.Clock.UtcNow = scenario.Clock.UtcNow.AddMinutes(2);
            (await FirstRankedAsync()).Should().Be(scenario.Second.Id);

            async Task<Guid> FirstRankedAsync()
            {
                using var response = await scenario.SendAsync(
                    HttpMethod.Get, $"/api/showcase?search={scenario.Search}&sort=trending&pageSize=1", scenario.Viewer);
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                var page = (await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseCardDto>>>())!.Data!;
                page.TotalItems.Should().Be(2);
                return page.Items.Single().Id;
            }
        });

    private async Task WithShowcasesAsync(Func<Scenario, Task> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var key = Guid.NewGuid().ToString("N");
        User CreateUser(string label)
        {
            var email = $"view-{label}-{key}@example.test";
            return new User { FullName = label, Email = email, NormalizedEmail = email.ToUpperInvariant() };
        }

        var owner = CreateUser("owner");
        var viewer = CreateUser("viewer");
        var otherViewer = CreateUser("other");
        db.Users.AddRange(owner, viewer, otherViewer);
        var courseId = await db.Courses.Select(course => course.Id).FirstAsync();
        var semesterId = await db.Semesters.Select(semester => semester.Id).FirstAsync();
        var classIndex = (await db.Classes.IgnoreQueryFilters()
            .Where(item => item.CourseId == courseId && item.SemesterId == semesterId)
            .MaxAsync(item => (int?)item.ClassIndex) ?? 0) + 1;
        var @class = new Class
        {
            ClassCode = $"View-{key}", Slug = $"view-{key}", ClassIndex = classIndex,
            CourseId = courseId, SemesterId = semesterId
        };
        var student = new Student { FullName = owner.FullName, User = owner };
        var enrollment = new ClassStudent { Class = @class, Student = student, CourseId = courseId, SemesterId = semesterId };
        var firstTeam = new Team { TeamCode = key[..8], TeamName = "View first team", Class = @class };
        firstTeam.TeamMembers.Add(new TeamMember { ClassStudent = enrollment });
        var secondTeam = new Team { TeamCode = key[8..16], TeamName = "View second team", Class = @class };
        var firstProject = new Project { Team = firstTeam, Name = $"View first {key}" };
        var secondProject = new Project { Team = secondTeam, Name = $"View second {key}" };
        var first = new ShowcaseProject
        {
            ProjectLineageId = firstProject.ProjectLineageId, Status = ShowcaseStatus.Published, PublishedAt = DateTime.UtcNow,
            ViewCount = 4
        };
        var second = new ShowcaseProject { ProjectLineageId = secondProject.ProjectLineageId, Status = ShowcaseStatus.Published, PublishedAt = DateTime.UtcNow };
        db.Projects.AddRange(firstProject, secondProject);
        db.ShowcaseProjects.AddRange(first, second);
        await db.SaveChangesAsync();

        var clock = new FakeClock();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(clock);
        }));
        using var client = host.CreateClient();
        try
        {
            await run(new Scenario(db, tokens, client, clock, key, first, second, owner, viewer, otherViewer));
        }
        finally
        {
            db.ChangeTracker.Clear();
            var showcaseIds = new[] { first.Id, second.Id };
            var projectIds = new[] { firstProject.Id, secondProject.Id };
            var teamIds = new[] { firstTeam.Id, secondTeam.Id };
            var userIds = new[] { owner.Id, viewer.Id, otherViewer.Id };
            await db.ShowcaseViewsDaily.Where(view => showcaseIds.Contains(view.ShowcaseProjectId)).ExecuteDeleteAsync();
            await db.TeamMembers.Where(member => teamIds.Contains(member.TeamId)).ExecuteDeleteAsync();
            await db.ClassStudents.Where(item => item.ClassId == @class.Id).ExecuteDeleteAsync();
            db.ShowcaseProjects.RemoveRange(await db.ShowcaseProjects.Where(item => showcaseIds.Contains(item.Id)).ToListAsync());
            db.Projects.RemoveRange(await db.Projects.Where(item => projectIds.Contains(item.Id)).ToListAsync());
            db.Teams.RemoveRange(await db.Teams.Where(item => teamIds.Contains(item.Id)).ToListAsync());
            db.Classes.Remove(await db.Classes.SingleAsync(item => item.Id == @class.Id));
            db.Students.Remove(await db.Students.SingleAsync(item => item.Id == student.Id));
            db.Users.RemoveRange(await db.Users.Where(item => userIds.Contains(item.Id)).ToListAsync());
            await db.SaveChangesAsync();
        }
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2026, 11, 6, 23, 59, 0, DateTimeKind.Utc);
    }

    private sealed record Scenario(
        AppDbContext Db, IJwtTokenService Tokens, HttpClient Client, FakeClock Clock, string Search,
        ShowcaseProject First, ShowcaseProject Second, User Owner, User Viewer, User OtherViewer)
    {
        public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, User user)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Tokens.GenerateAccessToken(user, [SystemRoles.Student]).Token);
            return await Client.SendAsync(request);
        }

        public async Task<int> ViewAsync(Guid id, User user)
        {
            using var response = await SendAsync(HttpMethod.Post, $"/api/showcase/{id}/view", user);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<ShowcaseViewDto>>())!.Data!.ViewCount;
        }
    }
}
