using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.ProjectData;

[Collection("Sequential")]
public sealed partial class ProjectDataIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task ProjectData_RejectsAnonymousAndNonStaffRoles()
    {
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/project-data")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/project-data/filter-options")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/project-data/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = new User { FullName = "No staff", Email = "pd-nostaff@example.test" };
        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor })
        {
            (await Send(client, user, role, HttpMethod.Get, "/api/project-data")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await Send(client, user, role, HttpMethod.Get, "/api/project-data/filter-options")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await Send(client, user, role, HttpMethod.Get, "/api/project-data/summary")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await Send(client, user, role, HttpMethod.Put, $"/api/project-data/{Guid.NewGuid()}/achievements",
                new UpdateProjectAchievementsRequest { RowVersion = "1" })).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }

        var anonymousPut = await client.PutAsJsonAsync($"/api/project-data/{Guid.NewGuid()}/achievements",
            new UpdateProjectAchievementsRequest { RowVersion = "1" });
        anonymousPut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_SeesRunningCompletedAndArchivedFromCompletedProjectsOnly()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var page = await GetPage(client, world.Admin, SystemRoles.Admin, $"search={world.Token}&sortBy=projectName");

            page.TotalItems.Should().Be(4);
            page.Items.Select(item => item.ProjectId).Should().BeEquivalentTo(
                [world.ProjectA, world.ProjectB, world.ProjectC, world.ProjectOutsider]);
            page.Items.Select(item => item.ProjectName).Should().NotContain(name =>
                name.StartsWith("Delta") || name.StartsWith("Echo") || name.StartsWith("Foxtrot"));
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task List_MapsGroupsIndustriesLecturerMentorsAndAchievements()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var page = await GetPage(client, world.Admin, SystemRoles.Admin, $"search={world.Token}");

            var active = page.Items.Single(item => item.ProjectId == world.ProjectA);
            active.SemesterCode.Should().NotBeNullOrEmpty();
            active.SubjectCode.Should().Be(world.CourseCode);
            active.ClassCode.Should().Be(world.ClassCodeA);
            active.Groups.Should().Equal($"{world.GroupPrefix}2", $"{world.GroupPrefix}10");
            active.StartupIndustries.Should().Equal("EdTech", "Health Tech");
            active.Lecturer!.UserId.Should().Be(world.Lecturer.Id);
            active.Mentor!.UserId.Should().Be(world.EnterpriseMentor.Id);
            active.Mentor.IsHistorical.Should().BeFalse();
            active.AcademicMentor!.UserId.Should().Be(world.AcademicMentor.Id);
            active.Achievements.Should().Equal("Potential", "Funded");
            active.RowVersion.Should().NotBeNullOrEmpty();

            var completed = page.Items.Single(item => item.ProjectId == world.ProjectB);
            completed.Groups.Should().BeEmpty();
            completed.StartupIndustries.Should().BeEmpty();
            completed.Mentor!.UserId.Should().Be(world.HistoricalMentor.Id);
            completed.Mentor.IsHistorical.Should().BeTrue();
            completed.Mentor.EndedAtUtc.Should().NotBeNull();
            completed.AcademicMentor.Should().BeNull("a mentor removed before completion must not appear as the final mentor");
            completed.Achievements.Should().BeEmpty();
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Lecturer_IsScopedToAssignedClassesForListCountsAndOptions()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();

            var primary = await GetPage(client, world.Lecturer, SystemRoles.Lecturer, $"search={world.Token}");
            primary.TotalItems.Should().Be(3);
            primary.Items.Select(item => item.ProjectId).Should().NotContain(world.ProjectOutsider);

            var co = await GetPage(client, world.CoLecturer, SystemRoles.Lecturer, $"search={world.Token}");
            co.Items.Select(item => item.ProjectId).Should().Equal(world.ProjectA);

            var outsider = await GetPage(client, world.Outsider, SystemRoles.Lecturer, $"search={world.Token}");
            outsider.Items.Select(item => item.ProjectId).Should().Equal(world.ProjectOutsider);

            // A lecturer cannot widen the scope through the lecturer filter.
            var spoofed = await GetPage(client, world.CoLecturer, SystemRoles.Lecturer,
                $"search={world.Token}&lecturerId={world.Outsider.Id}");
            spoofed.TotalItems.Should().Be(0);

            var options = await GetOptions(client, world.CoLecturer, SystemRoles.Lecturer);
            options.Subjects.Should().ContainSingle(item => item.Code == world.CourseCode);
            options.Subjects.Should().NotContain(item => item.Code == world.OtherCourseCode);
            options.Lecturers.Should().ContainSingle(item => item.UserId == world.Lecturer.Id);
            options.Lecturers.Should().NotContain(item => item.UserId == world.Outsider.Id);
            options.Groups.Should().Equal($"{world.GroupPrefix}2", $"{world.GroupPrefix}10");
            options.Mentors.Select(item => item.UserId).Should().BeEquivalentTo(
                [world.EnterpriseMentor.Id, world.AcademicMentor.Id]);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Filters_AndSearch_WorkIndependentlyAndTogether()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var baseQuery = $"search={world.Token}";

            (await Ids(client, world, $"{baseQuery}&group={world.GroupPrefix.ToLowerInvariant()}10")).Should().Equal(world.ProjectA);
            (await Ids(client, world, $"{baseQuery}&startupIndustry=health tech")).Should().Equal(world.ProjectA);
            (await Ids(client, world, $"{baseQuery}&subjectCode={world.OtherCourseCode.ToLowerInvariant()}"))
                .Should().Equal(world.ProjectOutsider);
            // Semester term and year are independent filters that combine with AND.
            var otherTerm = world.SemesterTerm == "FA" ? "SP" : "FA";
            (await Ids(client, world, $"{baseQuery}&semester={world.SemesterTerm.ToLowerInvariant()}")).Should().HaveCount(4);
            (await Ids(client, world, $"{baseQuery}&semester={otherTerm}")).Should().BeEmpty();
            (await Ids(client, world, $"{baseQuery}&year={world.SemesterYear}")).Should().HaveCount(4);
            (await Ids(client, world, $"{baseQuery}&year={world.SemesterYear + 1}")).Should().BeEmpty();
            (await Ids(client, world, $"{baseQuery}&semester={world.SemesterTerm}&year={world.SemesterYear}")).Should().HaveCount(4);
            (await Ids(client, world, $"{baseQuery}&semester={otherTerm}&year={world.SemesterYear}")).Should().BeEmpty();
            (await Ids(client, world, $"{baseQuery}&achievement=Funded")).Should().Equal(world.ProjectA);
            (await Ids(client, world, $"{baseQuery}&achievement=Awarded")).Should().BeEmpty();
            (await Ids(client, world, $"{baseQuery}&lecturerId={world.CoLecturer.Id}")).Should().Equal(world.ProjectA);
            (await Ids(client, world, $"{baseQuery}&mentorId={world.EnterpriseMentor.Id}")).Should().Equal(world.ProjectA);
            (await Ids(client, world, $"{baseQuery}&mentorId={world.AcademicMentor.Id}")).Should().Equal(world.ProjectA);
            // The historical mentor is the one displayed for the completed class, so the filter must match.
            (await Ids(client, world, $"{baseQuery}&mentorId={world.HistoricalMentor.Id}")).Should().Equal(world.ProjectB);
            // A mentor removed before completion is not displayed, so the filter must not match.
            (await Ids(client, world, $"{baseQuery}&mentorId={world.EarlyEndedMentor.Id}")).Should().BeEmpty();

            // Combined filters use AND.
            (await Ids(client, world, $"{baseQuery}&achievement=Potential&subjectCode={world.OtherCourseCode}"))
                .Should().BeEmpty();

            // Search covers description, industry tag, lecturer and displayed mentor names.
            (await Ids(client, world, $"search=quiet-{world.Token}")).Should().Equal(world.ProjectA);
            (await Ids(client, world, $"search={world.ClassCodeB}")).Should().Equal(world.ProjectB);
            (await Ids(client, world, $"search=edtech&subjectCode={world.CourseCode}")).Should().Equal(world.ProjectA);
            (await Ids(client, world, $"search={world.Token.ToUpperInvariant()}")).Should().HaveCount(4);
            (await Ids(client, world, $"search=outsider lecturer {world.Token}")).Should().Equal(world.ProjectOutsider);
            (await Ids(client, world, $"search=lecturer {world.Token}")).Should().HaveCount(4);
            (await Ids(client, world, $"search=historical mentor {world.Token}")).Should().Equal(world.ProjectB);
            (await Ids(client, world, $"search=early-ended mentor {world.Token}")).Should().BeEmpty();

            var industrySearch = await Ids(client, world, $"search=Health Tech");
            industrySearch.Should().Contain(world.ProjectA);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Pagination_Sorting_AreStableAndCountDistinctProjects()
    {
        var world = await SeedAsync(extraProjects: 12);
        try
        {
            using var client = factory.CreateClient();
            var baseQuery = $"search={world.Token}&pageSize=10";
            var query = $"{baseQuery}&sortBy=projectName";

            var first = await GetPage(client, world.Admin, SystemRoles.Admin, $"{query}&pageIndex=1");
            var second = await GetPage(client, world.Admin, SystemRoles.Admin, $"{query}&pageIndex=2");
            var beyond = await GetPage(client, world.Admin, SystemRoles.Admin, $"{query}&pageIndex=9");

            // 4 seeded + 12 extra projects; the multi-group, multi-industry, two-mentor project counts once.
            first.TotalItems.Should().Be(16);
            first.TotalPages.Should().Be(2);
            first.Items.Should().HaveCount(10);
            second.Items.Should().HaveCount(6);
            first.Items.Concat(second.Items).Select(item => item.ProjectId).Should().OnlyHaveUniqueItems();
            beyond.Items.Should().BeEmpty();
            beyond.TotalItems.Should().Be(16);

            var names = first.Items.Concat(second.Items).Select(item => item.ProjectName).ToList();
            names.Should().BeInAscendingOrder(StringComparer.Ordinal);

            var descending = await GetPage(client, world.Admin, SystemRoles.Admin,
                $"{baseQuery}&sortBy=projectName&isDescending=true");
            descending.Items.First().ProjectName.Should().Be(names.Max(StringComparer.Ordinal));

            foreach (var sortBy in new[] { "semester", "classCode", "group" })
            {
                var sorted = await GetPage(client, world.Admin, SystemRoles.Admin, $"{baseQuery}&sortBy={sortBy}");
                sorted.Items.Should().HaveCount(10);
            }
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Ordering_DefaultsToClassCodeThenNumericGroupWithUngroupedTeamsLast()
    {
        var world = await SeedAsync(groupedTeams: [11, 1, 3]);
        try
        {
            using var client = factory.CreateClient();
            var g = world.GroupedProjects;
            var search = $"search={world.Token}";

            // Class A (index 1) precedes B (2), C (3) and the other course; inside A the groups run G1, G2, G3, G11.
            (await Order(client, world, search)).Should().Equal(
                g[1], world.ProjectA, g[3], g[11], world.ProjectB, world.ProjectC, world.ProjectOutsider);
            (await Order(client, world, $"{search}&sortBy=classCode")).Should().Equal(
                g[1], world.ProjectA, g[3], g[11], world.ProjectB, world.ProjectC, world.ProjectOutsider);

            // Descending reverses only the class keys; groups inside a class stay ascending.
            (await Order(client, world, $"{search}&isDescending=true")).Should().Equal(
                world.ProjectOutsider, world.ProjectC, world.ProjectB, g[1], world.ProjectA, g[3], g[11]);

            // Sorting by group is numeric (G11 after G3); teams without a group always come last, by class code.
            (await Order(client, world, $"{search}&sortBy=group")).Should().Equal(
                g[1], world.ProjectA, g[3], g[11], world.ProjectB, world.ProjectC, world.ProjectOutsider);
            (await Order(client, world, $"{search}&sortBy=group&isDescending=true")).Should().Equal(
                g[11], g[3], world.ProjectA, g[1], world.ProjectB, world.ProjectC, world.ProjectOutsider);

            // The class code is shown as returned and the group list itself is in natural order.
            var page = await GetPage(client, world.Admin, SystemRoles.Admin, search);
            page.Items.Take(5).Select(item => item.ClassCode).Should().Equal(
                world.ClassCodeA, world.ClassCodeA, world.ClassCodeA, world.ClassCodeA, world.ClassCodeB);
            page.Items.Single(item => item.ProjectId == world.ProjectA).Groups
                .Should().Equal($"{world.GroupPrefix}2", $"{world.GroupPrefix}10");
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    private async Task<IReadOnlyList<Guid>> Order(HttpClient client, World world, string query)
    {
        var page = await GetPage(client, world.Admin, SystemRoles.Admin, query);
        return page.Items.Select(item => item.ProjectId).ToList();
    }

    [Fact]
    public async Task Summary_CountsGroupsAndAchievementsWithinScopeAndFilters()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var search = $"search={world.Token}";

            // Admin sees A (Potential + Funded), B, C and the other lecturer's E; nothing is Awarded yet.
            (await GetSummary(client, world.Admin, SystemRoles.Admin, search))
                .Should().BeEquivalentTo(new ProjectDataSummaryResponse
                { TotalGroups = 4, PotentialGroups = 1, FundedGroups = 1, AwardedGroups = 0 });

            // A lecturer only counts the classes they teach.
            (await GetSummary(client, world.Lecturer, SystemRoles.Lecturer, search))
                .Should().BeEquivalentTo(new ProjectDataSummaryResponse
                { TotalGroups = 3, PotentialGroups = 1, FundedGroups = 1, AwardedGroups = 0 });
            (await GetSummary(client, world.Outsider, SystemRoles.Lecturer, search))
                .Should().BeEquivalentTo(new ProjectDataSummaryResponse { TotalGroups = 1 });

            // Filters narrow the cards exactly like the table; paging and sorting are ignored.
            (await GetSummary(client, world.Admin, SystemRoles.Admin, $"{search}&subjectCode={world.OtherCourseCode}"))
                .Should().BeEquivalentTo(new ProjectDataSummaryResponse { TotalGroups = 1 });
            (await GetSummary(client, world.Admin, SystemRoles.Admin, $"{search}&achievement=Potential&pageIndex=7&pageSize=10&sortBy=group"))
                .Should().BeEquivalentTo(new ProjectDataSummaryResponse { TotalGroups = 1, PotentialGroups = 1, FundedGroups = 1 });
            (await GetSummary(client, world.Admin, SystemRoles.Admin, $"{search}&semester={(world.SemesterTerm == "FA" ? "SP" : "FA")}"))
                .Should().BeEquivalentTo(new ProjectDataSummaryResponse());

            // Labels are independent: a project can be counted in several cards, and the totals follow edits.
            var current = await CurrentAchievements(client, world, world.ProjectB);
            await PutAchievements(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectB,
                ["Potential", "Funded", "Awarded"], current.RowVersion);
            (await GetSummary(client, world.Admin, SystemRoles.Admin, search))
                .Should().BeEquivalentTo(new ProjectDataSummaryResponse
                { TotalGroups = 4, PotentialGroups = 2, FundedGroups = 2, AwardedGroups = 1 });
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    private async Task<ProjectDataSummaryResponse> GetSummary(HttpClient client, User user, string role, string query)
    {
        var response = await Send(client, user, role, HttpMethod.Get, $"/api/project-data/summary?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectDataSummaryResponse>>();
        return body!.Data!;
    }

    [Fact]
    public async Task InvalidParameters_ReturnBadRequest()
    {
        using var client = factory.CreateClient();
        var admin = new User { FullName = "PD admin", Email = "pd-admin-invalid@example.test" };
        foreach (var query in new[]
                 {
                     "pageIndex=0", "pageSize=7", "pageSize=1000", "sortBy=password", "sortBy=subject", "achievement=Famous", "semester=XX", "year=1999", "year=abc",
                     $"search={new string('x', 101)}",
                 })
        {
            var response = await Send(client, admin, SystemRoles.Admin, HttpMethod.Get, $"/api/project-data?{query}");
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, query);
        }
    }

    [Fact]
    public async Task FilterOptions_AreNotLimitedToCurrentPageOrFilters()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();
            var options = await GetOptions(client, world.Admin, SystemRoles.Admin);

            options.Subjects.Select(item => item.Code).Should().Contain([world.CourseCode, world.OtherCourseCode]);
            options.Groups.Should().Contain([$"{world.GroupPrefix}2", $"{world.GroupPrefix}10"]);
            options.StartupIndustries.Should().Contain(["EdTech", "Health Tech"]);
            options.Lecturers.Select(item => item.UserId).Should().Contain([world.Lecturer.Id, world.Outsider.Id]);
            options.Mentors.Should().Contain(item => item.UserId == world.HistoricalMentor.Id && item.Slot == "Enterprise");
            options.Mentors.Should().NotContain(item => item.UserId == world.EarlyEndedMentor.Id);
            options.Achievements.Should().Equal("Potential", "Funded", "Awarded");
            options.Years.Should().Contain(world.SemesterYear).And.BeInDescendingOrder();
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    private async Task<IReadOnlyList<Guid>> Ids(HttpClient client, World world, string query)
    {
        var page = await GetPage(client, world.Admin, SystemRoles.Admin, $"{query}&sortBy=projectName");
        return page.Items.Select(item => item.ProjectId).ToList();
    }

    private async Task<PagedResponse<ProjectDataItemResponse>> GetPage(
        HttpClient client, User user, string role, string query)
    {
        var response = await Send(client, user, role, HttpMethod.Get, $"/api/project-data?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<ProjectDataItemResponse>>>();
        return body!.Data!;
    }

    private async Task<ProjectDataFilterOptionsResponse> GetOptions(HttpClient client, User user, string role)
    {
        var response = await Send(client, user, role, HttpMethod.Get, "/api/project-data/filter-options");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectDataFilterOptionsResponse>>();
        return body!.Data!;
    }

    internal async Task<HttpResponseMessage> Send(
        HttpClient client, User user, string role, HttpMethod method, string url, object? body = null)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(user, [role]).Token;
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    internal sealed class World
    {
        public string Token { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public string OtherCourseCode { get; init; } = string.Empty;
        public string SemesterTerm { get; init; } = string.Empty;
        public int SemesterYear { get; init; }
        public string ClassCodeA { get; init; } = string.Empty;
        public string ClassCodeB { get; init; } = string.Empty;
        public string GroupPrefix { get; init; } = string.Empty;
        public Dictionary<int, Guid> GroupedProjects { get; init; } = [];
        public User Admin { get; init; } = null!;
        public User Lecturer { get; init; } = null!;
        public User CoLecturer { get; init; } = null!;
        public User Outsider { get; init; } = null!;
        public User EnterpriseMentor { get; init; } = null!;
        public User AcademicMentor { get; init; } = null!;
        public User HistoricalMentor { get; init; } = null!;
        public User EarlyEndedMentor { get; init; } = null!;
        public Guid ProjectA { get; init; }
        public Guid ProjectB { get; init; }
        public Guid ProjectC { get; init; }
        public Guid ProjectOutsider { get; init; }
        public Guid[] ClassIds { get; init; } = [];
    }

    internal async Task<World> SeedAsync(int extraProjects = 0, int[]? groupedTeams = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var token = Guid.NewGuid().ToString("N")[..8];
        var activeSemester = await context.Semesters
            .Where(semester => semester.Status == SemesterStatus.Active)
            .Select(semester => new { semester.Id, semester.Term, semester.Year })
            .FirstAsync();
        var semesterId = activeSemester.Id;

        User NewUser(string label) => new()
        {
            FullName = $"{label} {token}",
            Email = $"pd-{label.ToLowerInvariant().Replace(' ', '-')}-{token}@example.test",
            NormalizedEmail = $"PD-{label.ToUpperInvariant().Replace(' ', '-')}-{token}@EXAMPLE.TEST",
            PasswordHash = "integration-test-only",
        };

        var admin = NewUser("Admin");
        var lecturer = NewUser("Lecturer");
        var coLecturer = NewUser("Co lecturer");
        var outsider = NewUser("Outsider lecturer");
        var enterpriseMentor = NewUser("Mentor ent");
        var academicMentor = NewUser("Mentor acad");
        var historicalMentor = NewUser("Historical mentor");
        var earlyEndedMentor = NewUser("Early-ended mentor");
        context.Users.AddRange(admin, lecturer, coLecturer, outsider, enterpriseMentor, academicMentor,
            historicalMentor, earlyEndedMentor);

        MentorProfile Profile(User user, MentorType type) => new() { User = user, Type = type };
        var enterpriseProfile = Profile(enterpriseMentor, MentorType.Enterprise);
        var academicProfile = Profile(academicMentor, MentorType.Academic);
        var historicalProfile = Profile(historicalMentor, MentorType.Enterprise);
        var earlyEndedProfile = Profile(earlyEndedMentor, MentorType.Academic);
        context.MentorProfiles.AddRange(enterpriseProfile, academicProfile, historicalProfile, earlyEndedProfile);

        var course = new Course { Code = $"PD{token}".ToUpperInvariant(), Name = "Project data course", Status = CourseStatus.Active };
        var otherCourse = new Course { Code = $"PX{token}".ToUpperInvariant(), Name = "Other project data course", Status = CourseStatus.Active };
        var completedAt = DateTime.UtcNow.AddDays(-3);

        var classNumber = 0;
        Class NewClass(string suffix, Course classCourse, User primary, ClassStatus status) => new()
        {
            ClassCode = $"PD{token}-{suffix}",
            Slug = $"pd-{token}-{suffix.ToLowerInvariant()}",
            ClassIndex = ++classNumber,
            SemesterId = semesterId,
            Course = classCourse,
            PrimaryLecturerId = primary.Id,
            Status = status,
            ScheduleJson = "[{\"dayOfWeek\":1,\"startTime\":\"07:30\",\"endTime\":\"09:30\"}]",
        };

        var classA = NewClass("A", course, lecturer, ClassStatus.Active);
        // The database only allows IsPrimary rows in class_lecturers; this exercises the ClassLecturers scope branch.
        classA.ClassLecturers.Add(new ClassLecturer { LecturerId = coLecturer.Id, IsPrimary = true });
        var classB = NewClass("B", course, lecturer, ClassStatus.Completed);
        classB.CompletedAtUtc = completedAt;
        classB.CompletionReason = "Integration test completion";
        var classC = NewClass("C", course, lecturer, ClassStatus.Archived);
        classC.StatusBeforeArchive = ClassStatus.Completed;
        classC.CompletedAtUtc = completedAt;
        classC.CompletionReason = "Integration test completion";
        classC.ArchivedAtUtc = DateTime.UtcNow;
        var classD = NewClass("D", course, lecturer, ClassStatus.Archived);
        classD.StatusBeforeArchive = ClassStatus.Active;
        classD.ArchivedAtUtc = DateTime.UtcNow;
        var classE = NewClass("E", otherCourse, outsider, ClassStatus.Active);
        context.Classes.AddRange(classA, classB, classC, classD, classE);

        var teamNumber = 0;
        Team NewTeam(Class owner, TeamStatus status = TeamStatus.Active) => new()
        {
            Class = owner,
            TeamCode = $"T{token}{++teamNumber}",
            TeamName = $"Team {token} {teamNumber}",
            Status = status,
        };

        Project NewProject(Team team, string name, ProjectStatus status = ProjectStatus.Approved) => new()
        {
            Team = team,
            Name = name,
            Status = status,
        };

        var teamA = NewTeam(classA);
        var projectA = NewProject(teamA, $"Alpha {token} Health");
        projectA.Description = $"A quiet-{token} description";
        projectA.IsHighPotential = true;
        projectA.IsFunded = true;
        projectA.ProjectTags.Add(Tag("Health Tech"));
        projectA.ProjectTags.Add(Tag("EdTech"));
        projectA.ProjectTags.Add(new ProjectTag
        {
            TagName = $"keyword-{token}", NormalizedTagName = $"keyword-{token}", TagType = ProjectTagType.Keyword,
        });

        var teamB = NewTeam(classB);
        var projectB = NewProject(teamB, $"Bravo {token}");
        var teamC = NewTeam(classC);
        var projectC = NewProject(teamC, $"Charlie {token}");
        var teamD = NewTeam(classD);
        var projectD = NewProject(teamD, $"Delta {token}");
        var teamArchived = NewTeam(classA);
        var projectArchived = NewProject(teamArchived, $"Echo {token}", ProjectStatus.Archived);
        var teamDisabled = NewTeam(classA, TeamStatus.Disabled);
        var projectDisabled = NewProject(teamDisabled, $"Foxtrot {token}");
        var teamE = NewTeam(classE);
        var projectE = NewProject(teamE, $"Golf {token} Health");
        context.Projects.AddRange(projectA, projectB, projectC, projectD, projectArchived, projectDisabled, projectE);

        for (var index = 0; index < extraProjects; index++)
        {
            context.Projects.Add(NewProject(NewTeam(classA), $"Extra {token} {index:00}"));
        }

        var groupedTeamEntities = new List<(Team Team, int Number, Guid ProjectId)>();
        foreach (var number in groupedTeams ?? [])
        {
            var groupedTeam = NewTeam(classA);
            var groupedProject = NewProject(groupedTeam, $"Group {token} {number:00}");
            context.Projects.Add(groupedProject);
            groupedTeamEntities.Add((groupedTeam, number, groupedProject.Id));
        }

        var assigner = admin;
        MentorAssignment Assignment(Team team, MentorProfile profile, MentorType slot,
            MentorAssignmentStatus status, DateTime assignedAt, DateTime? endedAt) => new()
        {
            Team = team,
            MentorProfile = profile,
            AssignedBy = assigner,
            Slot = slot,
            Status = status,
            AssignedAt = assignedAt,
            EndedAt = endedAt,
        };

        var now = DateTime.UtcNow;
        context.MentorAssignments.AddRange(
            Assignment(teamA, enterpriseProfile, MentorType.Enterprise, MentorAssignmentStatus.Active, now.AddDays(-20), null),
            Assignment(teamA, academicProfile, MentorType.Academic, MentorAssignmentStatus.Active, now.AddDays(-20), null),
            // Class B was completed: the closing assignment is displayed, the one removed earlier is not,
            // and Pending/Cancelled assignments never are.
            Assignment(teamB, historicalProfile, MentorType.Enterprise, MentorAssignmentStatus.Ended, now.AddDays(-30), completedAt),
            Assignment(teamB, earlyEndedProfile, MentorType.Academic, MentorAssignmentStatus.Ended, now.AddDays(-30), now.AddDays(-12)),
            Assignment(teamB, enterpriseProfile, MentorType.Enterprise, MentorAssignmentStatus.Cancelled, now.AddDays(-2), null),
            Assignment(teamB, academicProfile, MentorType.Academic, MentorAssignmentStatus.Pending, now.AddDays(-2), null));

        var studentNumber = 0;
        void AddMember(Team team, Class owner, string? group)
        {
            var student = new Student
            {
                FullName = $"Student {token} {++studentNumber}",
                RollNumber = $"PD{token}{studentNumber}".ToUpperInvariant(),
                Status = StudentStatus.Active,
            };
            var enrollment = new ClassStudent
            {
                Class = owner,
                Student = student,
                SemesterId = semesterId,
                CourseId = owner.Course.Id,
                MajorCodeAtEnrollment = "SE",
                SemesterGroupName = group,
            };
            context.Students.Add(student);
            context.ClassStudents.Add(enrollment);
            context.TeamMembers.Add(new TeamMember
            {
                Team = team,
                ClassId = owner.Id,
                ClassStudent = enrollment,
                CountsTowardActiveTeam = true,
            });
        }
        // Duplicate group spelled differently, an empty group and a second group.
        var groupPrefix = $"{course.Code}g_1G";
        AddMember(teamA, classA, $"{groupPrefix}2");
        AddMember(teamA, classA, $" {groupPrefix.ToLowerInvariant()}2 ");
        AddMember(teamA, classA, null);
        AddMember(teamA, classA, $"{groupPrefix}10");
        AddMember(teamB, classB, "  ");
        foreach (var (groupedTeam, number, _) in groupedTeamEntities)
            AddMember(groupedTeam, classA, $"{groupPrefix}{number}");

        await context.SaveChangesAsync();

        return new World
        {
            Token = token,
            CourseCode = course.Code,
            OtherCourseCode = otherCourse.Code,
            SemesterTerm = activeSemester.Term switch { SemesterTerm.Spring => "SP", SemesterTerm.Summer => "SU", _ => "FA" },
            SemesterYear = activeSemester.Year,
            ClassCodeA = classA.ClassCode,
            ClassCodeB = classB.ClassCode,
            GroupPrefix = groupPrefix,
            GroupedProjects = groupedTeamEntities.ToDictionary(item => item.Number, item => item.ProjectId),
            Admin = admin,
            Lecturer = lecturer,
            CoLecturer = coLecturer,
            Outsider = outsider,
            EnterpriseMentor = enterpriseMentor,
            AcademicMentor = academicMentor,
            HistoricalMentor = historicalMentor,
            EarlyEndedMentor = earlyEndedMentor,
            ProjectA = projectA.Id,
            ProjectB = projectB.Id,
            ProjectC = projectC.Id,
            ProjectOutsider = projectE.Id,
            ClassIds = [classA.Id, classB.Id, classC.Id, classD.Id, classE.Id],
        };

        ProjectTag Tag(string name) => new()
        {
            TagName = name,
            NormalizedTagName = name.ToLowerInvariant(),
            TagType = ProjectTagType.StartupField,
        };
    }

    /// <summary>Soft-deletes the seeded classes, teams and projects so they never leak into other tests.</summary>
    internal async Task CleanupAsync(World world)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pattern = $"%{world.Token}%";
        await context.Projects.IgnoreQueryFilters().Where(project => EF.Functions.Like(project.Name, pattern))
            .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.IsDeleted, true));
        await context.Teams.IgnoreQueryFilters().Where(team => world.ClassIds.Contains(team.ClassId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(team => team.IsDeleted, true));
        await context.Classes.IgnoreQueryFilters().Where(item => world.ClassIds.Contains(item.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsDeleted, true));
    }
}
