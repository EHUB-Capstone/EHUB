using System.Net;
using System.Net.Http.Json;
using EHub.Contracts.Common;
using EHub.Contracts.ProjectData;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.ProjectData;

public sealed partial class ProjectDataIntegrationTests
{
    [Fact]
    public async Task Admin_CanSetAndClearAchievements_WithAuditAndRefreshedVersion()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var current = await CurrentAchievements(client, world, world.ProjectB);
            current.Achievements.Should().BeEmpty();

            var updated = await PutAchievements(client, world.Admin, SystemRoles.Admin, world.ProjectB,
                ["Funded", "awarded", "Funded"], current.RowVersion);
            updated.Achievements.Should().Equal("Funded", "Awarded");
            updated.RowVersion.Should().NotBe(current.RowVersion);

            var listed = await CurrentAchievements(client, world, world.ProjectB);
            listed.Achievements.Should().Equal("Funded", "Awarded");
            (await Ids(client, world, $"search={world.Token}&achievement=Awarded")).Should().Equal(world.ProjectB);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var project = await context.Projects.AsNoTracking().SingleAsync(item => item.Id == world.ProjectB);
                (project.IsFunded, project.IsAwarded, project.IsHighPotential).Should().Be((true, true, false));
                project.UpdatedBy.Should().Be(world.Admin.Id);

                var log = await context.ProjectActivityLogs.AsNoTracking()
                    .SingleAsync(item => item.ProjectId == world.ProjectB && item.Action == "PROJECT_ACHIEVEMENTS_CHANGED");
                log.ActorUserId.Should().Be(world.Admin.Id);
                log.Summary.Should().Contain("none").And.Contain("Funded, Awarded");
                log.OccurredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
            }

            // Existing Potential data is untouched by unrelated label changes, and labels can be cleared.
            var cleared = await PutAchievements(client, world.Admin, SystemRoles.Admin, world.ProjectB,
                [], updated.RowVersion);
            cleared.Achievements.Should().BeEmpty();
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task UnchangedLabelSet_IsNoOpWithoutAuditOrVersionChange()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var current = await CurrentAchievements(client, world, world.ProjectA);
            current.Achievements.Should().Equal("Potential", "Funded");

            var result = await PutAchievements(client, world.Admin, SystemRoles.Admin, world.ProjectA,
                ["Funded", "Potential"], current.RowVersion);
            result.RowVersion.Should().Be(current.RowVersion);

            await using var scope = factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await context.ProjectActivityLogs.CountAsync(item =>
                item.ProjectId == world.ProjectA && item.Action == "PROJECT_ACHIEVEMENTS_CHANGED")).Should().Be(0);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Lecturer_CanLabelOnlyProjectsOfAssignedClasses()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var own = await CurrentAchievements(client, world, world.ProjectB);
            var ownResponse = await Put(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB, ["Potential"], own.RowVersion);
            ownResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // A co-lecturer of class A may label A but not the completed class B they do not teach.
            var projectA = await CurrentAchievements(client, world, world.ProjectA);
            (await Put(client, world.CoLecturer, SystemRoles.Lecturer, world.ProjectA, ["Awarded"], projectA.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            var projectB = await CurrentAchievements(client, world, world.ProjectB);
            (await Put(client, world.CoLecturer, SystemRoles.Lecturer, world.ProjectB, ["Awarded"], projectB.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // IDOR: a lecturer of another class cannot label this project, and nothing changed.
            var outsiderResponse = await Put(client, world.Outsider, SystemRoles.Lecturer, world.ProjectA, ["Funded"], projectA.RowVersion);
            outsiderResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await CurrentAchievements(client, world, world.ProjectB)).Achievements.Should().Equal("Potential");
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task StaleVersion_ReturnsConflictAndKeepsExistingLabels()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var stale = await CurrentAchievements(client, world, world.ProjectB);
            await PutAchievements(client, world.Admin, SystemRoles.Admin, world.ProjectB, ["Funded"], stale.RowVersion);

            var response = await Put(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB, ["Awarded"], stale.RowVersion);
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            body!.Code.Should().Be("PROJECT_DATA_CONCURRENCY_CONFLICT");

            (await CurrentAchievements(client, world, world.ProjectB)).Achievements.Should().Equal("Funded");
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task UpdateAchievements_RejectsInvalidInputAndUnknownOrHiddenProjects()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var current = await CurrentAchievements(client, world, world.ProjectB);

            (await Put(client, world.Admin, SystemRoles.Admin, world.ProjectB, ["Famous"], current.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(client, world.Admin, SystemRoles.Admin, world.ProjectB, ["Potential"], "not-a-version"))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(client, world.Admin, SystemRoles.Admin, world.ProjectB,
                ["Potential", "Funded", "Awarded", "Potential"], current.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(client, world.Admin, SystemRoles.Admin, Guid.NewGuid(), ["Potential"], current.RowVersion))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);

            // Archived-from-active class and archived projects are outside the feature, even for admins.
            Guid hiddenProjectId;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                hiddenProjectId = await context.Projects.Where(item => item.Name == $"Delta {world.Token}")
                    .Select(item => item.Id).SingleAsync();
            }
            (await Put(client, world.Admin, SystemRoles.Admin, hiddenProjectId, ["Potential"], "1"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);

            (await CurrentAchievements(client, world, world.ProjectB)).Achievements.Should().BeEmpty();
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    private async Task<ProjectDataItemResponse> CurrentAchievements(HttpClient client, World world, Guid projectId)
    {
        var page = await GetPage(client, world.Admin, SystemRoles.Admin, $"search={world.Token}");
        return page.Items.Single(item => item.ProjectId == projectId);
    }

    private async Task<HttpResponseMessage> Put(
        HttpClient client, EHub.Domain.Entities.User user, string role, Guid projectId,
        string[] achievements, string rowVersion) =>
        await Send(client, user, role, HttpMethod.Put, $"/api/project-data/{projectId}/achievements",
            new UpdateProjectAchievementsRequest { Achievements = achievements, RowVersion = rowVersion });

    private async Task<ProjectAchievementsResponse> PutAchievements(
        HttpClient client, EHub.Domain.Entities.User user, string role, Guid projectId,
        string[] achievements, string rowVersion)
    {
        var response = await Put(client, user, role, projectId, achievements, rowVersion);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectAchievementsResponse>>();
        return body!.Data!;
    }
}
