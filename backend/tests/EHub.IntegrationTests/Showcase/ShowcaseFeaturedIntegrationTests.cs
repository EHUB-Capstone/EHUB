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
public sealed class ShowcaseFeaturedIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task FeaturedWorkflow_EnforcesScopeAndStates_PersistsAuditAndFilters_AndClearsOnHide()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = Guid.NewGuid().ToString("N");
        User NewUser(string label) => new() { FullName = label, Email = $"{label}-{token}@example.test", NormalizedEmail = $"{label}-{token}@example.test".ToUpperInvariant() };
        var admin = NewUser("Admin");
        var lecturer = NewUser("Lecturer");
        var outsider = NewUser("Outsider");
        db.Users.AddRange(admin, lecturer, outsider);
        var courseId = await db.Courses.Select(course => course.Id).FirstAsync();
        var semesterId = await db.Semesters.Select(semester => semester.Id).FirstAsync();
        var index = (await db.Classes.IgnoreQueryFilters().Where(item => item.CourseId == courseId && item.SemesterId == semesterId)
            .Select(item => (int?)item.ClassIndex).MaxAsync() ?? 0) + 1;
        var @class = new Class { ClassCode = $"Featured-{token}", Slug = $"featured-{token}", ClassIndex = index,
            CourseId = courseId, SemesterId = semesterId, PrimaryLecturerId = lecturer.Id };
        @class.ClassLecturers.Add(new ClassLecturer { LecturerId = lecturer.Id, IsPrimary = true });
        var team = new Team { TeamCode = token[..8], TeamName = "Featured test team", Class = @class };
        var project = new Project { Team = team, Name = $"Featured {token}", Description = "Faculty showcase integration fixture" };
        var showcase = new ShowcaseProject { ProjectLineageId = project.ProjectLineageId, Status = ShowcaseStatus.Published, PublishedAt = DateTime.UtcNow };
        db.Projects.Add(project);
        db.ShowcaseProjects.Add(showcase);
        await db.SaveChangesAsync();
        using var client = factory.CreateClient();

        async Task<HttpResponseMessage> Send(HttpMethod method, User user, string role, string suffix, object? body = null)
        {
            using var request = new HttpRequestMessage(method, $"/api/showcase/{showcase.Id}{suffix}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user, [role]).Token);
            if (body is not null) request.Content = JsonContent.Create(body);
            return await client.SendAsync(request);
        }
        async Task<ShowcaseFeaturedDto> Set(User user, string role, bool value)
        {
            using var response = await Send(value ? HttpMethod.Post : HttpMethod.Delete, user, role, "/feature");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<ShowcaseFeaturedDto>>())!.Data!;
        }
        async Task CheckDiscovery(bool expected)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/showcase?featured=true&search={token}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(admin, [SystemRoles.Admin]).Token);
            using var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var page = (await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ShowcaseCardDto>>>())!.Data!;
            page.TotalItems.Should().Be(expected ? 1 : 0);
            if (expected) page.Items.Should().ContainSingle().Which.IsFeatured.Should().BeTrue();
        }
        try
        {
            foreach (var method in new[] { HttpMethod.Post, HttpMethod.Delete })
            {
                using var denied = await Send(method, outsider, SystemRoles.Lecturer, "/feature");
                denied.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
            var featured = await Set(lecturer, SystemRoles.Lecturer, true);
            featured.IsFeatured.Should().BeTrue();
            featured.FeaturedAt.Should().NotBeNull();
            featured.FeaturedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
            var repeated = await Set(admin, SystemRoles.Admin, true);
            repeated.FeaturedAt.Should().Be(featured.FeaturedAt);
            repeated.RowVersion.Should().Be(featured.RowVersion);
            (await db.ShowcaseReviewHistories.CountAsync(item => item.ShowcaseProjectId == showcase.Id)).Should().Be(1);
            await CheckDiscovery(true);
            // Also exercise the ClassLecturers access path, without changing the single-primary rule.
            @class.PrimaryLecturerId = null;
            await db.SaveChangesAsync();
            var cleared = await Set(lecturer, SystemRoles.Lecturer, false);
            cleared.FeaturedAt.Should().BeNull();
            cleared.IsFeatured.Should().BeFalse();
            await Set(admin, SystemRoles.Admin, true);
            using (var hidden = await Send(HttpMethod.Post, lecturer, SystemRoles.Lecturer, "/hide", new ShowcaseDecisionRequest { Note = "Hidden for test review" }))
                hidden.StatusCode.Should().Be(HttpStatusCode.OK);
            var hiddenRow = await db.ShowcaseProjects.AsNoTracking().SingleAsync(item => item.Id == showcase.Id);
            hiddenRow.IsFeatured.Should().BeFalse();
            hiddenRow.FeaturedAt.Should().BeNull();
            await CheckDiscovery(false);
            foreach (var method in new[] { HttpMethod.Post, HttpMethod.Delete })
            {
                using var conflict = await Send(method, admin, SystemRoles.Admin, "/feature");
                conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
            }
            using (var restored = await Send(HttpMethod.Post, admin, SystemRoles.Admin, "/unhide", new ShowcaseDecisionRequest()))
                restored.StatusCode.Should().Be(HttpStatusCode.OK);
            (await db.ShowcaseProjects.AsNoTracking().SingleAsync(item => item.Id == showcase.Id)).IsFeatured.Should().BeFalse();
            var notes = await db.ShowcaseReviewHistories.Where(item => item.ShowcaseProjectId == showcase.Id).Select(item => item.Note).ToListAsync();
            notes.Should().Contain(note => note!.Contains("because the showcase was hidden"));
        }
        finally
        {
            db.ChangeTracker.Clear();
            db.ShowcaseProjects.Remove(await db.ShowcaseProjects.SingleAsync(item => item.Id == showcase.Id));
            db.Projects.Remove(await db.Projects.SingleAsync(item => item.Id == project.Id));
            db.Teams.Remove(await db.Teams.SingleAsync(item => item.Id == team.Id));
            db.Classes.Remove(await db.Classes.SingleAsync(item => item.Id == @class.Id));
            db.Users.RemoveRange(await db.Users.Where(item => new[] { admin.Id, lecturer.Id, outsider.Id }.Contains(item.Id)).ToListAsync());
            await db.SaveChangesAsync();
        }
    }
}
