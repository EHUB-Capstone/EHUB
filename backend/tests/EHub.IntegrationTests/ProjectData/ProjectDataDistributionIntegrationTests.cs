using System.Net;
using System.Net.Http.Json;
using EHub.Contracts.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EHub.Shared.Constants;

namespace EHub.IntegrationTests.ProjectData;

public sealed partial class ProjectDataIntegrationTests
{
    [Fact]
    public async Task Distribution_RejectsAnonymousAndNonStaffRoles()
    {
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/project-data/distribution")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = new User { FullName = "No staff", Email = "pd-dist-nostaff@example.test" };
        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor })
        {
            (await Send(client, user, role, HttpMethod.Get, "/api/project-data/distribution")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Distribution_GroupsBySemesterMajorIndustryAndOutcomeWithinScopeAndFilters()
    {
        var world = await SeedAsync();
        try
        {
            await UseValidMajorsForProjectAAsync(world);
            using var client = factory.CreateClient();
            var search = $"search={world.Token}";

            // Admin sees A, B, C and the other lecturer's E.
            var admin = await GetDistribution(client, world.Admin, SystemRoles.Admin, search);
            admin.TotalGroups.Should().Be(4);
            admin.Semesters.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Year = world.SemesterYear, Term = world.SemesterTerm, Count = 4,
            });
            // Project A has four counting members: BIT_SE, BBA_MKT, UNDECLARED with a BIT_AI profile, and the invalid "SE".
            // B has one invalid member and C and E have none, so those three projects and A's invalid member are unspecified.
            admin.Majors.Select(bucket => (bucket.Name, bucket.Count)).Should().Equal(
                ("BBA_MKT", 1), ("BIT_AI", 1), ("BIT_SE", 1), (ProjectDataDistributionRules.UnspecifiedMajor, 4));
            admin.StartupIndustries.Select(bucket => (bucket.Name, bucket.Count)).Should().Equal(("EdTech", 1), ("Health Tech", 1));
            admin.UnspecifiedStartupIndustryCount.Should().Be(3);
            admin.Outcomes.Select(bucket => (bucket.Name, bucket.Count)).Should().Equal(
                ("Potential", 1), ("Internally Funded", 1), ("Externally Funded", 0), ("Awarded", 0));
            admin.NoOutcomeCount.Should().Be(3);

            // A lecturer only counts the classes they teach, like the summary.
            var lecturer = await GetDistribution(client, world.Lecturer, SystemRoles.Lecturer, search);
            lecturer.TotalGroups.Should().Be(3);

            // Every filter keeps the totals and outcome counts in step with the summary cards.
            foreach (var query in new[]
            {
                search,
                $"{search}&subjectCode={world.OtherCourseCode}",
                $"{search}&achievement=Potential",
                $"{search}&startupIndustry=health tech",
                $"{search}&semester={world.SemesterTerm}&year={world.SemesterYear}",
                $"{search}&semester={(world.SemesterTerm == "FA" ? "SP" : "FA")}",
                $"{search}&pageIndex=7&pageSize=10&sortBy=group",
            })
            {
                var distribution = await GetDistribution(client, world.Admin, SystemRoles.Admin, query);
                var summary = await GetSummary(client, world.Admin, SystemRoles.Admin, query);
                distribution.TotalGroups.Should().Be(summary.TotalGroups, query);
                distribution.Semesters.Sum(bucket => bucket.Count).Should().Be(summary.TotalGroups, query);
                distribution.Outcomes.Single(bucket => bucket.Name == "Potential").Count.Should().Be(summary.PotentialGroups, query);
                distribution.Outcomes.Single(bucket => bucket.Name == "Internally Funded").Count.Should().Be(summary.InternallyFundedGroups, query);
                distribution.Outcomes.Single(bucket => bucket.Name == "Externally Funded").Count.Should().Be(summary.ExternallyFundedGroups, query);
                distribution.Outcomes.Single(bucket => bucket.Name == "Awarded").Count.Should().Be(summary.AwardedGroups, query);
            }

            var filtered = await GetDistribution(client, world.Admin, SystemRoles.Admin, $"{search}&achievement=Potential");
            filtered.TotalGroups.Should().Be(1);
            filtered.NoOutcomeCount.Should().Be(0);

            var none = await GetDistribution(client, world.Admin, SystemRoles.Admin,
                $"{search}&semester={(world.SemesterTerm == "FA" ? "SP" : "FA")}");
            none.TotalGroups.Should().Be(0);
            none.Semesters.Should().BeEmpty();
            none.Majors.Should().BeEmpty();
            none.StartupIndustries.Should().BeEmpty();
            none.UnspecifiedStartupIndustryCount.Should().Be(0);
            none.NoOutcomeCount.Should().Be(0);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }


    [Fact]
    public async Task MajorFilter_KeepsListSummaryAndDistributionInStep()
    {
        var world = await SeedAsync();
        try
        {
            await UseValidMajorsForProjectAAsync(world);
            using var client = factory.CreateClient();
            var search = $"search={world.Token}";

            // Project A has BIT_SE, BBA_MKT, BIT_AI (profile fallback) and an invalid member, B has an invalid member and
            // C and E have no members, so UNSPECIFIED matches all four.
            foreach (var (major, expected) in new[]
            {
                ("BIT_SE", 1), ("bit_se", 1), ("BBA_MKT", 1), ("BIT_AI", 1), ("BEN", 0),
                (ProjectDataDistributionRules.UnspecifiedMajor, 4),
            })
            {
                var query = $"{search}&major={major}";
                var page = await GetPage(client, world.Admin, SystemRoles.Admin, query);
                var summary = await GetSummary(client, world.Admin, SystemRoles.Admin, query);
                var distribution = await GetDistribution(client, world.Admin, SystemRoles.Admin, query);

                page.TotalItems.Should().Be(expected, query);
                summary.TotalGroups.Should().Be(expected, query);
                distribution.TotalGroups.Should().Be(expected, query);
            }

            (await GetPage(client, world.Admin, SystemRoles.Admin, $"{search}&major=BIT_AI")).Items
                .Select(item => item.ProjectId).Should().Equal(world.ProjectA);

            // A lecturer's scope still applies on top of the filter.
            (await GetPage(client, world.Lecturer, SystemRoles.Lecturer, $"{search}&major=UNSPECIFIED")).TotalItems.Should().Be(3);

            // Narrowing by major does not hide the other majors of the matching project.
            var narrowed = await GetDistribution(client, world.Admin, SystemRoles.Admin, $"{search}&major=BIT_SE");
            narrowed.Majors.Select(bucket => bucket.Name).Should().Contain(["BIT_SE", "BBA_MKT", "BIT_AI"]);

            foreach (var url in new[] { "/api/project-data", "/api/project-data/summary", "/api/project-data/distribution" })
            {
                (await Send(client, world.Admin, SystemRoles.Admin, HttpMethod.Get, $"{url}?{search}&major=NOPE"))
                    .StatusCode.Should().Be(HttpStatusCode.BadRequest, url);
            }

            (await GetOptions(client, world.Admin, SystemRoles.Admin)).Majors
                .Should().Contain(["BIT_SE", "BBA_MKT", ProjectDataDistributionRules.UnspecifiedMajor]);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    /// <summary>The shared seed uses the invalid major "SE"; give three of project A's members real codes.</summary>
    private async Task UseValidMajorsForProjectAAsync(World world)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var enrollments = await context.ClassStudents.Include(item => item.Student)
            .Where(item => item.ClassId == world.ClassIds[0] && item.Student.RollNumber != null)
            .OrderBy(item => item.Student.RollNumber)
            .ToListAsync();
        enrollments.Should().HaveCount(4);

        enrollments[0].MajorCodeAtEnrollment = MajorCodes.BIT_SE;
        enrollments[1].MajorCodeAtEnrollment = MajorCodes.BBA_MKT;
        enrollments[2].MajorCodeAtEnrollment = MajorCodes.Undeclared;
        enrollments[2].Student.MajorCode = MajorCodes.BIT_AI;
        await context.SaveChangesAsync();
    }

    private async Task<ProjectDataDistributionResponse> GetDistribution(HttpClient client, User user, string role, string query)
    {
        var response = await Send(client, user, role, HttpMethod.Get, $"/api/project-data/distribution?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectDataDistributionResponse>>();
        return body!.Data!;
    }
}
