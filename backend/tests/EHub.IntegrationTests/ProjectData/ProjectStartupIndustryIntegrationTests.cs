using System.Net;
using System.Net.Http.Json;
using EHub.Contracts.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.ProjectData;

public sealed partial class ProjectDataIntegrationTests
{
    [Fact]
    public async Task Admin_CanReplaceStartupIndustries_WithAuditHistoryAndRefreshedVersion()
    {
        var world = await SeedAsync();
        var industries = await SeedIndustriesAsync(world, active: 3, inactive: 1);
        try
        {
            using var client = factory.CreateClient();
            var current = await CurrentAchievements(client, world, world.ProjectB);
            current.StartupIndustries.Should().BeEmpty();

            var first = await PutIndustries(client, world.Admin, SystemRoles.Admin, world.ProjectB,
                [industries.Active[1].Id, industries.Active[0].Id, industries.Active[1].Id], current.RowVersion);
            first.StartupIndustries.Should().Equal(industries.Active[0].Name, industries.Active[1].Name);
            first.RowVersion.Should().NotBe(current.RowVersion);

            var listed = await CurrentAchievements(client, world, world.ProjectB);
            listed.StartupIndustries.Should().Equal(industries.Active[0].Name, industries.Active[1].Name);
            listed.RowVersion.Should().Be(first.RowVersion);
            (await Ids(client, world, $"search={world.Token}&startupIndustry={Uri.EscapeDataString(industries.Active[0].Name)}"))
                .Should().Equal(world.ProjectB);

            // Replacing one industry keeps the other and is recorded with who changed what.
            var second = await PutIndustries(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB,
                [industries.Active[1].Id, industries.Active[2].Id], first.RowVersion);
            second.StartupIndustries.Should().Equal(industries.Active[1].Name, industries.Active[2].Name);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var tags = await context.ProjectTags.AsNoTracking()
                    .Where(tag => tag.ProjectId == world.ProjectB && tag.TagType == ProjectTagType.StartupField)
                    .Select(tag => tag.TagName).ToListAsync();
                tags.Should().BeEquivalentTo(second.StartupIndustries);

                var project = await context.Projects.AsNoTracking().SingleAsync(item => item.Id == world.ProjectB);
                project.UpdatedBy.Should().Be(world.Lecturer.Id);

                var logs = await context.ProjectActivityLogs.AsNoTracking()
                    .Where(item => item.ProjectId == world.ProjectB && item.Action == "PROJECT_STARTUP_INDUSTRIES_CHANGED")
                    .OrderBy(item => item.OccurredAtUtc).ToListAsync();
                logs.Should().HaveCount(2);
                logs[1].ActorUserId.Should().Be(world.Lecturer.Id);
                logs[1].Summary.Should().Contain("changed from").And.Contain(industries.Active[0].Name);
                logs[1].OccurredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
            }

            var history = await GetIndustryHistory(client, world.Admin, SystemRoles.Admin, world.ProjectB);
            history.TotalCount.Should().Be(2);
            var latest = history.Items.First();
            latest.ActorName.Should().Be(world.Lecturer.FullName);
            latest.Added.Should().Equal(industries.Active[2].Name);
            latest.Removed.Should().Equal(industries.Active[0].Name);
            latest.Kept.Should().Equal(industries.Active[1].Name);
            history.Items.Last().Added.Should().BeEquivalentTo(first.StartupIndustries);

            // The achievements history is a separate stream and stays empty.
            var achievementHistory = await Send(client, world.Admin, SystemRoles.Admin, HttpMethod.Get,
                $"/api/project-data/{world.ProjectB}/achievements/history");
            (await achievementHistory.Content.ReadFromJsonAsync<ApiResponse<ProjectAchievementHistoryResponse>>())!
                .Data!.TotalCount.Should().Be(0);
        }
        finally
        {
            await CleanupIndustriesAsync(industries);
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task UnchangedStartupIndustries_AreANoOpWithoutHistoryOrVersionChange()
    {
        var world = await SeedAsync();
        var industries = await SeedIndustriesAsync(world, active: 2, inactive: 0);
        try
        {
            using var client = factory.CreateClient();
            var current = await CurrentAchievements(client, world, world.ProjectB);
            var saved = await PutIndustries(client, world.Admin, SystemRoles.Admin, world.ProjectB,
                [industries.Active[0].Id, industries.Active[1].Id], current.RowVersion);

            // A retry with the version it already received (or an outdated one for the same set) must not log again.
            var retry = await PutIndustries(client, world.Admin, SystemRoles.Admin, world.ProjectB,
                [industries.Active[1].Id, industries.Active[0].Id], saved.RowVersion);
            retry.RowVersion.Should().Be(saved.RowVersion);
            (await GetIndustryHistory(client, world.Admin, SystemRoles.Admin, world.ProjectB)).TotalCount.Should().Be(1);
        }
        finally
        {
            await CleanupIndustriesAsync(industries);
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Lecturer_CanChangeIndustriesOnlyForProjectsOfAssignedClasses()
    {
        var world = await SeedAsync();
        var industries = await SeedIndustriesAsync(world, active: 2, inactive: 0);
        try
        {
            using var client = factory.CreateClient();
            var own = await CurrentAchievements(client, world, world.ProjectB);
            (await PutIndustriesRaw(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB, [industries.Active[0].Id], own.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            // A co-lecturer of class A may not change class B, a lecturer of another class may not change A: IDOR is closed.
            var projectB = await CurrentAchievements(client, world, world.ProjectB);
            (await PutIndustriesRaw(client, world.CoLecturer, SystemRoles.Lecturer, world.ProjectB, [industries.Active[1].Id], projectB.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var projectA = await CurrentAchievements(client, world, world.ProjectA);
            (await PutIndustriesRaw(client, world.Outsider, SystemRoles.Lecturer, world.ProjectA, [industries.Active[1].Id], projectA.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Send(client, world.Outsider, SystemRoles.Lecturer, HttpMethod.Get,
                    $"/api/project-data/{world.ProjectA}/startup-industries/history"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await CurrentAchievements(client, world, world.ProjectA)).StartupIndustries.Should().Equal("EdTech", "Health Tech");
            (await CurrentAchievements(client, world, world.ProjectB)).StartupIndustries.Should().Equal(industries.Active[0].Name);
        }
        finally
        {
            await CleanupIndustriesAsync(industries);
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task StartupIndustryUpdate_RejectsAnonymousAndNonStaffRoles()
    {
        using var client = factory.CreateClient();
        var url = $"/api/project-data/{Guid.NewGuid()}/startup-industries";
        var body = new UpdateProjectStartupIndustriesRequest { StartupIndustryIds = [Guid.NewGuid()], RowVersion = "1" };
        (await client.PutAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync(url + "/history")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = new User { FullName = "No staff", Email = "pd-industry-nostaff@example.test" };
        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor })
        {
            (await Send(client, user, role, HttpMethod.Put, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Send(client, user, role, HttpMethod.Get, url + "/history")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task StaleVersion_ReturnsConflictAndKeepsExistingIndustries()
    {
        var world = await SeedAsync();
        var industries = await SeedIndustriesAsync(world, active: 2, inactive: 0);
        try
        {
            using var client = factory.CreateClient();
            var stale = await CurrentAchievements(client, world, world.ProjectB);
            await PutIndustries(client, world.Admin, SystemRoles.Admin, world.ProjectB, [industries.Active[0].Id], stale.RowVersion);

            var response = await PutIndustriesRaw(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB, [industries.Active[1].Id], stale.RowVersion);
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await response.Content.ReadFromJsonAsync<ApiResponse<object>>())!.Code.Should().Be("PROJECT_DATA_CONCURRENCY_CONFLICT");

            (await CurrentAchievements(client, world, world.ProjectB)).StartupIndustries.Should().Equal(industries.Active[0].Name);
        }
        finally
        {
            await CleanupIndustriesAsync(industries);
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task UpdateStartupIndustries_RejectsInvalidInputAndUnknownProjects()
    {
        var world = await SeedAsync();
        var industries = await SeedIndustriesAsync(world, active: 4, inactive: 1);
        try
        {
            using var client = factory.CreateClient();
            var current = await CurrentAchievements(client, world, world.ProjectB);
            var admin = world.Admin;

            (await PutIndustriesRaw(client, admin, SystemRoles.Admin, world.ProjectB, [], current.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PutIndustriesRaw(client, admin, SystemRoles.Admin, world.ProjectB,
                industries.Active.Select(item => item.Id).ToArray(), current.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PutIndustriesRaw(client, admin, SystemRoles.Admin, world.ProjectB, [Guid.Empty], current.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PutIndustriesRaw(client, admin, SystemRoles.Admin, world.ProjectB, [Guid.NewGuid()], current.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PutIndustriesRaw(client, admin, SystemRoles.Admin, world.ProjectB, [industries.Inactive[0].Id], current.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PutIndustriesRaw(client, admin, SystemRoles.Admin, world.ProjectB, [industries.Active[0].Id], "not-a-version"))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await PutIndustriesRaw(client, admin, SystemRoles.Admin, Guid.NewGuid(), [industries.Active[0].Id], current.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await Send(client, admin, SystemRoles.Admin, HttpMethod.Get, $"/api/project-data/{Guid.NewGuid()}/startup-industries/history"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);

            (await CurrentAchievements(client, world, world.ProjectB)).StartupIndustries.Should().BeEmpty();
            (await GetIndustryHistory(client, admin, SystemRoles.Admin, world.ProjectB)).TotalCount.Should().Be(0);
        }
        finally
        {
            await CleanupIndustriesAsync(industries);
            await CleanupAsync(world);
        }
    }

    private sealed record SeededIndustries(IReadOnlyList<StartupIndustry> Active, IReadOnlyList<StartupIndustry> Inactive)
    {
        public IEnumerable<Guid> AllIds => Active.Concat(Inactive).Select(item => item.Id);
    }

    private async Task<SeededIndustries> SeedIndustriesAsync(World world, int active, int inactive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        StartupIndustry Make(string kind, int index, StartupIndustryStatus status)
        {
            var name = $"{kind}{index} {world.Token}";
            return new StartupIndustry { Name = name, NormalizedName = name.ToUpperInvariant(), Status = status };
        }

        var activeIndustries = Enumerable.Range(0, active).Select(index => Make("Ind", index, StartupIndustryStatus.Active)).ToList();
        var inactiveIndustries = Enumerable.Range(0, inactive).Select(index => Make("Off", index, StartupIndustryStatus.Inactive)).ToList();
        context.StartupIndustries.AddRange(activeIndustries.Concat(inactiveIndustries));
        await context.SaveChangesAsync();
        return new SeededIndustries(activeIndustries, inactiveIndustries);
    }

    private async Task CleanupIndustriesAsync(SeededIndustries industries)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = industries.AllIds.ToArray();
        await context.StartupIndustries.IgnoreQueryFilters().Where(item => ids.Contains(item.Id)).ExecuteDeleteAsync();
    }

    private async Task<HttpResponseMessage> PutIndustriesRaw(
        HttpClient client, User user, string role, Guid projectId, Guid[] industryIds, string rowVersion) =>
        await Send(client, user, role, HttpMethod.Put, $"/api/project-data/{projectId}/startup-industries",
            new UpdateProjectStartupIndustriesRequest { StartupIndustryIds = industryIds, RowVersion = rowVersion });

    private async Task<ProjectStartupIndustriesResponse> PutIndustries(
        HttpClient client, User user, string role, Guid projectId, Guid[] industryIds, string rowVersion)
    {
        var response = await PutIndustriesRaw(client, user, role, projectId, industryIds, rowVersion);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProjectStartupIndustriesResponse>>())!.Data!;
    }

    private async Task<ProjectStartupIndustryHistoryResponse> GetIndustryHistory(
        HttpClient client, User user, string role, Guid projectId)
    {
        var response = await Send(client, user, role, HttpMethod.Get, $"/api/project-data/{projectId}/startup-industries/history");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProjectStartupIndustryHistoryResponse>>())!.Data!;
    }
}
