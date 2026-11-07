using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Showcase;

[Collection("Sequential")]
public sealed class ShowcaseUpvoteIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public Task Upvote_IsIdempotentPerShowcase_AndDiscoveryAndDetailReflectTheViewer() =>
        WithShowcasesAsync(async scenario =>
        {
            var firstId = scenario.First.Id;
            var secondId = scenario.Second.Id;
            var viewer = scenario.Viewer;

            (await scenario.VoteAsync(firstId, viewer, HttpMethod.Delete)).Should()
                .BeEquivalentTo(new ShowcaseUpvoteDto { LikeCount = 0, HasUpvoted = false });

            await scenario.VoteAsync(firstId, viewer, HttpMethod.Post);
            var firstVote = await scenario.Db.ShowcaseUpvotes.AsNoTracking()
                .SingleAsync(vote => vote.ShowcaseProjectId == firstId && vote.UserId == viewer.Id);
            (await scenario.VoteAsync(firstId, viewer, HttpMethod.Post)).Should()
                .BeEquivalentTo(new ShowcaseUpvoteDto { LikeCount = 1, HasUpvoted = true });
            (await scenario.Db.ShowcaseUpvotes.AsNoTracking()
                .SingleAsync(vote => vote.ShowcaseProjectId == firstId && vote.UserId == viewer.Id))
                .CreatedAt.Should().Be(firstVote.CreatedAt);

            (await scenario.VoteAsync(secondId, viewer, HttpMethod.Post)).LikeCount.Should().Be(1);
            var cards = await scenario.ReadAsync<PagedResponse<ShowcaseCardDto>>($"/api/showcase?search={scenario.Search}", viewer);
            cards.Items.Should().HaveCount(2).And.OnlyContain(card => card.HasUpvoted && card.LikeCount == 1 && !card.IsOwner);
            var detailPath = $"/api/showcase/{firstId}";
            var detail = await scenario.ReadAsync<ShowcaseDetailDto>(detailPath, viewer);
            detail.HasUpvoted.Should().BeTrue();
            detail.Showcase.LikeCount.Should().Be(1);
            (await scenario.ReadAsync<ShowcaseDetailDto>(detailPath, scenario.OtherViewer)).HasUpvoted.Should().BeFalse();

            var ownerCards = await scenario.ReadAsync<PagedResponse<ShowcaseCardDto>>($"/api/showcase?search={scenario.Search}", scenario.Owner);
            ownerCards.Items.Single(card => card.Id == firstId).IsOwner.Should().BeTrue();
            ownerCards.Items.Single(card => card.Id == secondId).IsOwner.Should().BeFalse();

            for (var attempt = 0; attempt < 2; attempt++)
                (await scenario.VoteAsync(firstId, viewer, HttpMethod.Delete)).Should()
                    .BeEquivalentTo(new ShowcaseUpvoteDto { LikeCount = 0, HasUpvoted = false });

            (await scenario.ReadAsync<ShowcaseDetailDto>(detailPath, viewer)).HasUpvoted.Should().BeFalse();
            (await scenario.ReadAsync<ShowcaseDetailDto>($"/api/showcase/{secondId}", viewer)).HasUpvoted.Should().BeTrue();
            (await scenario.VoteAsync(firstId, viewer, HttpMethod.Post)).LikeCount.Should().Be(1);
        });

    [Fact]
    public Task Upvote_RejectsOwners_AndTreatsUnpublishedAndMissingPagesAsNotFound() =>
        WithShowcasesAsync(async scenario =>
        {
            var firstPath = $"/api/showcase/{scenario.First.Id}/upvote";
            using var forbidden = await scenario.SendAsync(HttpMethod.Post, firstPath, scenario.Owner);
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await scenario.ReadAsync<ShowcaseDetailDto>($"/api/showcase/{scenario.First.Id}", scenario.Owner))
                .Showcase.LikeCount.Should().Be(0);

            foreach (var status in new[] { ShowcaseStatus.Draft, ShowcaseStatus.PendingReview, ShowcaseStatus.Hidden })
            {
                await scenario.Db.ShowcaseProjects.Where(item => item.Id == scenario.Second.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, status));
                foreach (var method in new[] { HttpMethod.Post, HttpMethod.Delete })
                {
                    using var unpublished = await scenario.SendAsync(method, $"/api/showcase/{scenario.Second.Id}/upvote", scenario.Viewer);
                    unpublished.StatusCode.Should().Be(HttpStatusCode.NotFound);
                }
            }

            using var missing = await scenario.SendAsync(HttpMethod.Post, $"/api/showcase/{Guid.NewGuid()}/upvote", scenario.Viewer);
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        });

    [Fact]
    public Task Upvote_AllowsStaffAndMentors_AndUsesRoleForOwnershipInReads() =>
        WithShowcasesAsync(async scenario =>
        {
            var id = scenario.First.Id;
            foreach (var role in new[] { SystemRoles.Lecturer, SystemRoles.Mentor, SystemRoles.Admin })
            {
                var cards = await scenario.ReadAsync<PagedResponse<ShowcaseCardDto>>(
                    $"/api/showcase?search={scenario.Search}", scenario.Owner, role: role);
                cards.Items.Single(card => card.Id == id).IsOwner.Should().BeFalse();
                (await scenario.VoteAsync(id, scenario.Owner, HttpMethod.Post, role))
                    .Should().BeEquivalentTo(new ShowcaseUpvoteDto { LikeCount = 1, HasUpvoted = true });
                var detail = await scenario.ReadAsync<ShowcaseDetailDto>($"/api/showcase/{id}", scenario.Owner, role: role);
                detail.IsOwner.Should().BeFalse();
                detail.HasUpvoted.Should().BeTrue();
                (await scenario.VoteAsync(id, scenario.Owner, HttpMethod.Delete, role)).LikeCount.Should().Be(0);
            }

            using var dualRole = await scenario.SendAsync(
                HttpMethod.Post, $"/api/showcase/{id}/upvote", scenario.Owner, SystemRoles.Student, SystemRoles.Mentor);
            dualRole.StatusCode.Should().Be(HttpStatusCode.OK);
        });

    [Fact]
    public Task Trending_RanksByRecentVotesBeforePaging_AndUpdatesAfterRemoval() =>
        WithShowcasesAsync(async scenario =>
        {
            var now = DateTime.UtcNow;
            scenario.First.PublishedAt = now.AddDays(-6);
            // A weight of two would tie both projects at six, putting the newer project first.
            scenario.Second.PublishedAt = now.AddHours(-55);
            scenario.First.LikeCount = 2;
            scenario.Second.LikeCount = 3;
            scenario.Db.ShowcaseUpvotes.AddRange(
                Vote(scenario.First.Id, scenario.Viewer.Id, now.AddHours(-2)),
                Vote(scenario.First.Id, scenario.OtherViewer.Id, now.AddHours(-3)),
                Vote(scenario.Second.Id, scenario.Viewer.Id, now.AddHours(-30)),
                Vote(scenario.Second.Id, scenario.OtherViewer.Id, now.AddHours(-31)),
                Vote(scenario.Second.Id, scenario.Owner.Id, now.AddHours(-32)));
            await scenario.Db.SaveChangesAsync();

            var path = $"/api/showcase?search={scenario.Search}&pageSize=1";
            var first = await ReadPageAsync("sort=trending");
            first.TotalItems.Should().Be(2);
            first.TotalPages.Should().Be(2);
            first.Items.Should().ContainSingle().Which.Id.Should().Be(scenario.First.Id);
            (await ReadPageAsync("sort=trending&page=2")).Items.Should().ContainSingle().Which.Id.Should().Be(scenario.Second.Id);
            (await ReadPageAsync("sort=popular")).Items.Should().ContainSingle().Which.Id.Should().Be(scenario.Second.Id);

            await scenario.VoteAsync(scenario.First.Id, scenario.Viewer, HttpMethod.Delete);
            (await ReadPageAsync("sort=trending")).Items.Should().ContainSingle().Which.Id.Should().Be(scenario.Second.Id);

            Task<PagedResponse<ShowcaseCardDto>> ReadPageAsync(string query) =>
                scenario.ReadAsync<PagedResponse<ShowcaseCardDto>>($"{path}&{query}", scenario.Viewer);

            static ShowcaseUpvote Vote(Guid showcaseId, Guid userId, DateTime createdAt) =>
                new() { ShowcaseProjectId = showcaseId, UserId = userId, CreatedAt = createdAt };
        });

    [Fact]
    public Task ConcurrentUpvotesAndRemovals_KeepCounterEqualToStoredVotes() =>
        WithShowcasesAsync(async scenario =>
        {
            var id = scenario.First.Id;
            var viewers = Enumerable.Repeat(scenario.Viewer, 4).Concat(Enumerable.Repeat(scenario.OtherViewer, 4)).ToArray();

            var added = await Task.WhenAll(viewers.Select(user => scenario.VoteAsync(id, user, HttpMethod.Post)));
            added.Should().OnlyContain(result => result.HasUpvoted && result.LikeCount >= 1 && result.LikeCount <= 2);
            await AssertCounterAsync(2);

            var removed = await Task.WhenAll(viewers.Select(user => scenario.VoteAsync(id, user, HttpMethod.Delete)));
            removed.Should().OnlyContain(result => !result.HasUpvoted && result.LikeCount >= 0 && result.LikeCount <= 1);
            await AssertCounterAsync(0);

            async Task AssertCounterAsync(int expected)
            {
                (await scenario.Db.ShowcaseUpvotes.CountAsync(vote => vote.ShowcaseProjectId == id)).Should().Be(expected);
                (await scenario.Db.ShowcaseProjects.Where(item => item.Id == id).Select(item => item.LikeCount).SingleAsync())
                    .Should().Be(expected);
            }
        });

    private async Task WithShowcasesAsync(Func<Scenario, Task> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var key = Guid.NewGuid().ToString("N");

        User NewUser(string label)
        {
            var email = $"upvote-{label}-{key}@example.test";
            return new User { FullName = label, Email = email, NormalizedEmail = email.ToUpperInvariant() };
        }

        var owner = NewUser("owner");
        var viewer = NewUser("viewer");
        var otherViewer = NewUser("other");
        db.Users.AddRange(owner, viewer, otherViewer);
        var courseId = await db.Courses.Select(course => course.Id).FirstAsync();
        var semesterId = await db.Semesters.Select(semester => semester.Id).FirstAsync();
        var classIndex = (await db.Classes.IgnoreQueryFilters()
            .Where(item => item.CourseId == courseId && item.SemesterId == semesterId)
            .MaxAsync(item => (int?)item.ClassIndex) ?? 0) + 1;
        var @class = new Class
        {
            ClassCode = $"Upvote-{key}", Slug = $"upvote-{key}", ClassIndex = classIndex,
            CourseId = courseId, SemesterId = semesterId
        };
        var student = new Student { FullName = owner.FullName, User = owner };
        var enrollment = new ClassStudent { Class = @class, Student = student, CourseId = courseId, SemesterId = semesterId };
        var firstTeam = new Team { TeamCode = key[..8], TeamName = "Upvote first team", Class = @class };
        firstTeam.TeamMembers.Add(new TeamMember { ClassStudent = enrollment });
        var secondTeam = new Team { TeamCode = key[8..16], TeamName = "Upvote second team", Class = @class };
        var firstProject = new Project { Team = firstTeam, Name = $"Upvote first {key}" };
        var secondProject = new Project { Team = secondTeam, Name = $"Upvote second {key}" };
        var first = new ShowcaseProject { ProjectLineageId = firstProject.ProjectLineageId, Status = ShowcaseStatus.Published, PublishedAt = DateTime.UtcNow };
        var second = new ShowcaseProject { ProjectLineageId = secondProject.ProjectLineageId, Status = ShowcaseStatus.Published, PublishedAt = DateTime.UtcNow };
        db.Projects.AddRange(firstProject, secondProject);
        db.ShowcaseProjects.AddRange(first, second);
        await db.SaveChangesAsync();

        using var client = factory.CreateClient();
        try
        {
            await run(new Scenario(db, tokens, client, key, first, second, owner, viewer, otherViewer));
        }
        finally
        {
            db.ChangeTracker.Clear();
            var showcaseIds = new[] { first.Id, second.Id };
            var projectIds = new[] { firstProject.Id, secondProject.Id };
            var teamIds = new[] { firstTeam.Id, secondTeam.Id };
            var userIds = new[] { owner.Id, viewer.Id, otherViewer.Id };
            await db.ShowcaseUpvotes.Where(vote => showcaseIds.Contains(vote.ShowcaseProjectId)).ExecuteDeleteAsync();
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

    private sealed record Scenario(
        AppDbContext Db, IJwtTokenService Tokens, HttpClient Client, string Search,
        ShowcaseProject First, ShowcaseProject Second, User Owner, User Viewer, User OtherViewer)
    {
        public async Task<HttpResponseMessage> SendAsync(
            HttpMethod method, string path, User user, string role = SystemRoles.Student, string? additionalRole = null)
        {
            using var request = new HttpRequestMessage(method, path);
            string[] roles = additionalRole is null ? [role] : [role, additionalRole];
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Tokens.GenerateAccessToken(user, roles).Token);
            return await Client.SendAsync(request);
        }

        public Task<ShowcaseUpvoteDto> VoteAsync(Guid id, User user, HttpMethod method, string role = SystemRoles.Student) =>
            ReadAsync<ShowcaseUpvoteDto>($"/api/showcase/{id}/upvote", user, method, role);

        public async Task<T> ReadAsync<T>(string path, User user, HttpMethod? method = null, string role = SystemRoles.Student)
        {
            using var response = await SendAsync(method ?? HttpMethod.Get, path, user, role);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>())!.Data!;
        }
    }
}
