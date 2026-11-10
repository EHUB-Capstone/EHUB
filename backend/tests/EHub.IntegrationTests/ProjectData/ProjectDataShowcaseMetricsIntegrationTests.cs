using System.Net;
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
    public async Task ShowcaseMetrics_ReadCurrentCountersWithinStaffScope_AndPreserveFiltersSortingAndPaging()
    {
        var world = await SeedAsync(extraProjects: 12);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = new[] { world.ProjectA, world.ProjectB, world.ProjectOutsider };
        var lineages = await db.Projects.Where(project => ids.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id, project => project.ProjectLineageId);
        var first = new ShowcaseProject
        {
            ProjectLineageId = lineages[world.ProjectA], Status = ShowcaseStatus.Published,
            ViewCount = 120, LikeCount = 15, CommentCount = 7
        };
        var zero = new ShowcaseProject { ProjectLineageId = lineages[world.ProjectB], Status = ShowcaseStatus.Draft };
        var outside = new ShowcaseProject
        {
            ProjectLineageId = lineages[world.ProjectOutsider], Status = ShowcaseStatus.Hidden,
            ViewCount = 999, LikeCount = 88, CommentCount = 66
        };
        try
        {
            using var client = factory.CreateClient();
            var query = $"search={world.Token}&sortBy=projectName&pageSize=10";
            var before = await GetPage(client, world.Admin, SystemRoles.Admin, query);
            db.ShowcaseProjects.AddRange(first, zero, outside);
            await db.SaveChangesAsync();
            var after = await GetPage(client, world.Admin, SystemRoles.Admin, query);
            after.Items.Select(item => item.ProjectId).Should().Equal(before.Items.Select(item => item.ProjectId));
            (after.TotalItems, after.TotalPages, after.PageIndex, after.PageSize)
                .Should().Be((before.TotalItems, before.TotalPages, before.PageIndex, before.PageSize));
            var all = await GetPage(client, world.Admin, SystemRoles.Admin, $"search={world.Token}&pageSize=100");
            all.Items.Single(item => item.ProjectId == world.ProjectA).ShowcaseMetrics.Should()
                .BeEquivalentTo(new ProjectDataShowcaseMetricsResponse { ViewCount = 120, UpvoteCount = 15, CommentCount = 7 });
            all.Items.Single(item => item.ProjectId == world.ProjectB).ShowcaseMetrics.Should()
                .BeEquivalentTo(new ProjectDataShowcaseMetricsResponse());
            all.Items.Single(item => item.ProjectId == world.ProjectC).ShowcaseMetrics.Should().BeNull();
            all.Items.Single(item => item.ProjectId == world.ProjectOutsider).ShowcaseMetrics!.ViewCount.Should().Be(999);
            var lecturer = await GetPage(client, world.Lecturer, SystemRoles.Lecturer, $"search={world.Token}&pageSize=100");
            lecturer.Items.Should().NotContain(item => item.ProjectId == world.ProjectOutsider);
            lecturer.Items.Single(item => item.ProjectId == world.ProjectA).ShowcaseMetrics!.UpvoteCount.Should().Be(15);
            var coLecturer = await GetPage(client, world.CoLecturer, SystemRoles.Lecturer, $"search={world.Token}&pageSize=100");
            coLecturer.Items.Should().NotContain(item => item.ProjectId == world.ProjectB || item.ProjectId == world.ProjectOutsider);
            coLecturer.Items.Single(item => item.ProjectId == world.ProjectA).ShowcaseMetrics!.CommentCount.Should().Be(7);
            var filtered = await GetPage(client, world.Admin, SystemRoles.Admin,
                $"search={world.Token}&startupIndustry=Health%20Tech&sortBy=projectName");
            filtered.Items.Should().ContainSingle().Which.ProjectId.Should().Be(world.ProjectA);
            filtered.Items.Single().ShowcaseMetrics!.ViewCount.Should().Be(120);
            // Reads return the existing counters, not counts reconstructed from activity tables.
            await db.ShowcaseProjects.Where(page => page.Id == first.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(page => page.LikeCount, 16));
            var refreshed = await GetPage(client, world.Admin, SystemRoles.Admin, query);
            refreshed.Items.Single(item => item.ProjectId == world.ProjectA).ShowcaseMetrics!.UpvoteCount.Should().Be(16);
            foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor })
            {
                using var forbidden = await Send(client, world.EnterpriseMentor, role, HttpMethod.Get, $"/api/project-data?{query}");
                forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
                (await forbidden.Content.ReadAsStringAsync()).Should().NotContain("showcaseMetrics");
            }
            using var anonymous = await client.GetAsync($"/api/project-data?{query}");
            anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await db.ShowcaseProjects.Where(page => new[] { first.Id, zero.Id, outside.Id }.Contains(page.Id))
                .ExecuteUpdateAsync(update => update.SetProperty(page => page.IsDeleted, true));
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task ShowcaseMetrics_FollowLineageAcrossSemesters_IncludeHiddenPages_AndExcludeSoftDeletedShowcases()
    {
        var world = await SeedAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await db.Projects.SingleAsync(project => project.Id == world.ProjectA);
        var historical = await db.Projects.SingleAsync(project => project.Id == world.ProjectB);
        // Project B belongs to the completed class. Its lineage now continues as Project A.
        historical.ProjectLineageId = first.ProjectLineageId;
        var showcase = new ShowcaseProject
        {
            ProjectLineageId = first.ProjectLineageId, Status = ShowcaseStatus.Hidden,
            ViewCount = 42, LikeCount = 3, CommentCount = 2
        };
        db.ShowcaseProjects.Add(showcase);
        await db.SaveChangesAsync();
        try
        {
            using var client = factory.CreateClient();
            var query = $"search={world.Token}&pageSize=100";
            var page = await GetPage(client, world.Admin, SystemRoles.Admin, query);
            var metrics = page.Items.Single(item => item.ProjectId == first.Id).ShowcaseMetrics;
            metrics.Should().NotBeNull();
            page.Items.Single(item => item.ProjectId == historical.Id).ShowcaseMetrics.Should().BeEquivalentTo(metrics);
            foreach (var status in new[] { ShowcaseStatus.Draft, ShowcaseStatus.PendingReview, ShowcaseStatus.Published, ShowcaseStatus.Hidden })
            {
                await db.ShowcaseProjects.Where(item => item.Id == showcase.Id)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, status));
                var states = await GetPage(client, world.Lecturer, SystemRoles.Lecturer, query);
                states.Items.Single(item => item.ProjectId == first.Id).ShowcaseMetrics!.ViewCount.Should().Be(42);
            }
            await db.ShowcaseProjects.Where(item => item.Id == showcase.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDeleted, true));
            var removed = await GetPage(client, world.Admin, SystemRoles.Admin, query);
            removed.Items.Single(item => item.ProjectId == first.Id).ShowcaseMetrics.Should().BeNull();
            removed.Items.Single(item => item.ProjectId == historical.Id).ShowcaseMetrics.Should().BeNull();
            // A recreated showcase in the same lineage is the sole current source.
            var replacement = new ShowcaseProject { ProjectLineageId = first.ProjectLineageId, ViewCount = 5 };
            db.ShowcaseProjects.Add(replacement);
            await db.SaveChangesAsync();
            var recreated = await GetPage(client, world.Admin, SystemRoles.Admin, query);
            recreated.Items.Single(item => item.ProjectId == historical.Id).ShowcaseMetrics!.ViewCount.Should().Be(5);
        }
        finally
        {
            await db.ShowcaseProjects.Where(page => page.ProjectLineageId == first.ProjectLineageId)
                .ExecuteUpdateAsync(update => update.SetProperty(page => page.IsDeleted, true));
            await CleanupAsync(world);
        }
    }
}
