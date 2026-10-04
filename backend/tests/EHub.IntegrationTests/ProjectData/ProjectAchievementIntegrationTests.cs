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
    public async Task Note_IsStoredWithWhoAndWhen_ShownInTheListAndSearchable()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var before = DateTime.UtcNow.AddSeconds(-5);
            var current = await CurrentAchievements(client, world, world.ProjectB);
            current.AchievementNote.Should().BeNull();
            current.AchievementsUpdatedBy.Should().BeNull();

            // The lecturer of the class marks the project and explains why (the note is trimmed).
            var saved = await PutAchievements(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB,
                ["Potential"], current.RowVersion, $"  Strong pilot with 3 paying customers {world.Token}  ");
            saved.Note.Should().Be($"Strong pilot with 3 paying customers {world.Token}");
            saved.UpdatedBy!.UserId.Should().Be(world.Lecturer.Id);
            saved.UpdatedBy.FullName.Should().Be(world.Lecturer.FullName);
            saved.UpdatedAtUtc.Should().BeAfter(before);
            saved.UpdatedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);

            var listed = await CurrentAchievements(client, world, world.ProjectB);
            listed.AchievementNote.Should().Be(saved.Note);
            listed.AchievementsUpdatedBy!.FullName.Should().Be(world.Lecturer.FullName);
            listed.AchievementsUpdatedAtUtc.Should().BeCloseTo(saved.UpdatedAtUtc!.Value, TimeSpan.FromSeconds(1));
            (await Ids(client, world, "search=paying customers " + world.Token)).Should().Equal(world.ProjectB);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var log = await context.ProjectActivityLogs.AsNoTracking()
                    .SingleAsync(item => item.ProjectId == world.ProjectB && item.Action == "PROJECT_ACHIEVEMENTS_CHANGED");
                log.Summary.Should().Contain("from none to Potential").And.Contain("Strong pilot");
                log.Summary.Length.Should().BeLessThanOrEqualTo(300);
                log.ChangedFieldsJson.Should().Contain("Potential").And.Contain("note");
            }

            // Only the note changes: it is saved, attributed to the new editor and logged on its own.
            var noteOnly = await PutAchievements(client, world.Admin, SystemRoles.Admin, world.ProjectB,
                ["Potential"], saved.RowVersion, "Revised note");
            noteOnly.RowVersion.Should().NotBe(saved.RowVersion);
            noteOnly.Note.Should().Be("Revised note");
            noteOnly.UpdatedBy!.UserId.Should().Be(world.Admin.Id);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                (await context.ProjectActivityLogs.AsNoTracking()
                    .Where(item => item.ProjectId == world.ProjectB && item.Action == "PROJECT_ACHIEVEMENTS_CHANGED")
                    .Select(item => item.Summary).ToListAsync())
                    .Should().Contain(summary => summary.StartsWith("Achievement note updated."));
            }

            // Saving the very same labels and note again changes nothing.
            var again = await PutAchievements(client, world.Admin, SystemRoles.Admin, world.ProjectB,
                ["Potential"], noteOnly.RowVersion, "  Revised note ");
            again.RowVersion.Should().Be(noteOnly.RowVersion);

            // Removing every label clears the note but keeps who removed them and when.
            var cleared = await PutAchievements(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB,
                [], noteOnly.RowVersion);
            cleared.Achievements.Should().BeEmpty();
            cleared.Note.Should().BeNull();
            cleared.UpdatedBy!.UserId.Should().Be(world.Lecturer.Id);
            (await Ids(client, world, "search=Revised note")).Should().NotContain(world.ProjectB);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task History_ListsEveryChangeNewestFirstWithinTheCallersScope()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            (await History(client, world.Admin, SystemRoles.Admin, world.ProjectB)).TotalCount.Should().Be(0);

            var first = await PutAchievements(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB,
                ["Potential"], (await CurrentAchievements(client, world, world.ProjectB)).RowVersion, "First reason");
            var second = await PutAchievements(client, world.Admin, SystemRoles.Admin, world.ProjectB,
                ["Potential", "Funded"], first.RowVersion, "Second reason");
            await PutAchievements(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB,
                [], second.RowVersion);

            var history = await History(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB);
            history.TotalCount.Should().Be(3);
            history.Items.Select(item => item.ActorName).Should().Equal(
                world.Lecturer.FullName, world.Admin.FullName, world.Lecturer.FullName);
            history.Items.Select(item => item.OccurredAtUtc).Should().BeInDescendingOrder();
            history.Items.Select(item => item.Action).Should().OnlyContain(action => action == "PROJECT_ACHIEVEMENTS_CHANGED");
            history.Items.Select(item => item.Summary).Should().SatisfyRespectively(
                latest => latest.Should().Contain("from Potential, Funded to none"),
                middle => middle.Should().Contain("Funded").And.Contain("Second reason"),
                oldest => oldest.Should().Contain("from none to Potential").And.Contain("First reason"));

            // The structured fields drive the display: what was added, removed and kept, and the note per entry.
            var (latest, middle, oldest) = (history.Items.ElementAt(0), history.Items.ElementAt(1), history.Items.ElementAt(2));
            (latest.Added, latest.Removed, latest.Kept).Should().BeEquivalentTo((Array.Empty<string>(), new[] { "Potential", "Funded" }, Array.Empty<string>()));
            (latest.NoteChanged, latest.Note).Should().Be((true, (string?)null));
            (middle.Added, middle.Removed, middle.Kept).Should().BeEquivalentTo((new[] { "Funded" }, Array.Empty<string>(), new[] { "Potential" }));
            (middle.NoteChanged, middle.Note).Should().Be((true, "Second reason"));
            (oldest.Added, oldest.Removed, oldest.Kept).Should().BeEquivalentTo((new[] { "Potential" }, Array.Empty<string>(), Array.Empty<string>()));
            (oldest.NoteChanged, oldest.Note).Should().Be((true, "First reason"));

            // Unchanged saves add nothing; other projects have their own history.
            (await History(client, world.Admin, SystemRoles.Admin, world.ProjectA)).TotalCount.Should().Be(0);

            // Scope: a lecturer of another class cannot read it, and unknown projects are not found.
            (await Send(client, world.Outsider, SystemRoles.Lecturer, HttpMethod.Get,
                $"/api/project-data/{world.ProjectB}/achievements/history")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Send(client, world.Admin, SystemRoles.Admin, HttpMethod.Get,
                $"/api/project-data/{Guid.NewGuid()}/achievements/history")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await Send(client, world.Admin, SystemRoles.Student, HttpMethod.Get,
                $"/api/project-data/{world.ProjectB}/achievements/history")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/project-data/{world.ProjectB}/achievements/history"))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task History_ReadsEntriesWrittenBeforeTheStructuredFormat()
    {
        var world = await SeedAsync();
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var start = DateTime.UtcNow.AddDays(-3);
                context.ProjectActivityLogs.AddRange(
                    new EHub.Domain.Entities.ProjectActivityLog
                    {
                        ProjectId = world.ProjectB, ActorUserId = world.Admin.Id, Action = "PROJECT_ACHIEVEMENTS_CHANGED",
                        Summary = "Achievements changed from none to Potential. Note: \"Old style note\"",
                        ChangedFieldsJson = "[\"Potential\",\"note\"]", OccurredAtUtc = start,
                    },
                    new EHub.Domain.Entities.ProjectActivityLog
                    {
                        ProjectId = world.ProjectB, ActorUserId = world.Admin.Id, Action = "PROJECT_ACHIEVEMENTS_CHANGED",
                        Summary = "Achievements changed from Potential to Potential, Awarded.",
                        ChangedFieldsJson = "[\"Awarded\"]", OccurredAtUtc = start.AddHours(1),
                    });
                await context.SaveChangesAsync();
            }

            using var client = factory.CreateClient();
            var items = (await History(client, world.Admin, SystemRoles.Admin, world.ProjectB)).Items.ToList();
            items[1].Added.Should().Equal("Potential");
            (items[1].NoteChanged, items[1].Note).Should().Be((true, "Old style note"));
            items[0].Added.Should().Equal("Awarded");
            items[0].Kept.Should().Equal("Potential");
            items[0].NoteChanged.Should().BeFalse();
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task History_ReturnsOnlyTheLatestEntriesButReportsTheTotal()
    {
        var world = await SeedAsync();
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var start = DateTime.UtcNow.AddDays(-60);
                for (var index = 0; index < 55; index++)
                {
                    context.ProjectActivityLogs.Add(new EHub.Domain.Entities.ProjectActivityLog
                    {
                        ProjectId = world.ProjectB,
                        ActorUserId = world.Admin.Id,
                        Action = index % 10 == 0 ? "ACHIEVEMENTS_CARRIED_OVER" : "PROJECT_ACHIEVEMENTS_CHANGED",
                        Summary = $"Entry {index:00}",
                        OccurredAtUtc = start.AddHours(index),
                    });
                }
                // Other kinds of project activity are not part of the achievement history.
                context.ProjectActivityLogs.Add(new EHub.Domain.Entities.ProjectActivityLog
                {
                    ProjectId = world.ProjectB, Action = "PROJECT_PROFILE_UPDATED", Summary = "Profile edit",
                    OccurredAtUtc = DateTime.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            using var client = factory.CreateClient();
            var history = await History(client, world.Admin, SystemRoles.Admin, world.ProjectB);
            history.TotalCount.Should().Be(55);
            history.Items.Should().HaveCount(50);
            history.Items.First().Summary.Should().Be("Entry 54");
            history.Items.Last().Summary.Should().Be("Entry 05");
            history.Items.Should().NotContain(item => item.Summary == "Profile edit");
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    private async Task<ProjectAchievementHistoryResponse> History(
        HttpClient client, EHub.Domain.Entities.User user, string role, Guid projectId)
    {
        var response = await Send(client, user, role, HttpMethod.Get, $"/api/project-data/{projectId}/achievements/history");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectAchievementHistoryResponse>>();
        return body!.Data!;
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
            // A note describes labels, so it needs at least one, and it has a length limit.
            (await Put(client, world.Admin, SystemRoles.Admin, world.ProjectB, [], current.RowVersion, "Orphan note"))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Put(client, world.Admin, SystemRoles.Admin, world.ProjectB, ["Potential"], current.RowVersion, new string('n', 501)))
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
        string[] achievements, string rowVersion, string? note = null) =>
        await Send(client, user, role, HttpMethod.Put, $"/api/project-data/{projectId}/achievements",
            new UpdateProjectAchievementsRequest { Achievements = achievements, RowVersion = rowVersion, Note = note });

    private async Task<ProjectAchievementsResponse> PutAchievements(
        HttpClient client, EHub.Domain.Entities.User user, string role, Guid projectId,
        string[] achievements, string rowVersion, string? note = null)
    {
        var response = await Put(client, user, role, projectId, achievements, rowVersion, note);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectAchievementsResponse>>();
        return body!.Data!;
    }
}
