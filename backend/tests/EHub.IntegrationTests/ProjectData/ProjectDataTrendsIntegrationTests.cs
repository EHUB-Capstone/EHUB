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
    public async Task Trends_RejectsAnonymousAndNonStaffRoles()
    {
        using var client = factory.CreateClient();
        (await client.GetAsync("/api/project-data/trends")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = new User { FullName = "No staff", Email = "pd-trend-nostaff@example.test" };
        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor })
        {
            (await Send(client, user, role, HttpMethod.Get, "/api/project-data/trends")).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Trends_CompareOfficialScoresAndOutcomesAcrossSemestersWithoutRewritingHistory()
    {
        var world = await SeedTrendWorldAsync();
        try
        {
            using var client = factory.CreateClient();
            var search = $"search={world.Token}";

            var trends = await GetTrends(client, world.Admin, SystemRoles.Admin, search);

            // Old semester: T1 scores 7.6 and T2 scores 6.0. New semester: T3 scores 9.0, while T4 has a draft checkpoint
            // evaluation and is not graded yet. Draft and submitted evaluations never reach the official metrics.
            trends.Semesters.Should().HaveCount(2);
            var (older, newer) = (trends.Semesters.First(), trends.Semesters.Last());
            older.SemesterCode.Should().Be(world.OldSemesterCode);
            (older.TotalGroups, older.GradedGroups, older.AverageScore).Should().Be((2, 2, 6.8m));
            (older.PotentialGroups, older.InternallyFundedGroups, older.AwardedGroups).Should().Be((1, 0, 0));
            older.AverageScoreChange.Should().BeNull();
            newer.SemesterCode.Should().Be(world.NewSemesterCode);
            (newer.TotalGroups, newer.GradedGroups, newer.AverageScore).Should().Be((2, 1, 9.0m));
            (newer.PotentialGroups, newer.InternallyFundedGroups, newer.AwardedGroups).Should().Be((1, 1, 0));
            (newer.AverageScoreChange, newer.PotentialChange, newer.InternallyFundedChange, newer.AwardedChange)
                .Should().Be((2.2m, 0, 1, 0));

            // Subject and class level carry the same context.
            trends.Subjects.Select(row => (row.SemesterCode, row.SubjectCode, row.AverageScore)).Should().Equal(
                (world.OldSemesterCode, world.CourseCode, 6.8m), (world.NewSemesterCode, world.CourseCode, 9.0m));
            trends.Subjects.Last().AverageScoreChange.Should().Be(2.2m);
            trends.Classes.Select(row => (row.SemesterCode, row.ClassCode, row.GradedGroups)).Should().Equal(
                (world.OldSemesterCode, world.OldClassCode, 2), (world.NewSemesterCode, world.NewClassCode, 1));

            // A trend always spans semesters, so semester and year filters are ignored.
            var filtered = await GetTrends(client, world.Admin, SystemRoles.Admin, $"{search}&semester=SU&year=2005");
            filtered.Semesters.Select(row => (row.SemesterCode, row.AverageScore))
                .Should().Equal(trends.Semesters.Select(row => (row.SemesterCode, row.AverageScore)));

            // Other filters still narrow it, like the table.
            (await GetTrends(client, world.Admin, SystemRoles.Admin, $"{search}&achievement=Internally%20Funded")).Semesters
                .Select(row => (row.SemesterCode, row.TotalGroups)).Should().Equal((world.NewSemesterCode, 1));

            // Lecturers only see classes they teach.
            (await GetTrends(client, world.Lecturer, SystemRoles.Lecturer, search)).Semesters.Should().HaveCount(2);
            (await GetTrends(client, world.Outsider, SystemRoles.Lecturer, search)).Semesters.Should().BeEmpty();

            // Continuing a project into the newer semester copies its labels there, but the old semester is unchanged.
            await ContinueProjectAsync(world);
            var continued = await GetTrends(client, world.Admin, SystemRoles.Admin, search);
            continued.Semesters.First().Should().BeEquivalentTo(older);
            continued.Semesters.Last().TotalGroups.Should().Be(3);
            continued.Semesters.Last().PotentialGroups.Should().Be(2);

            // Unpublishing a component takes the group out of the official score.
            await SetEvaluationStatusAsync(world.NewAssessmentEvaluationId, EvaluationStatus.Submitted);
            var unpublished = await GetTrends(client, world.Admin, SystemRoles.Admin, search);
            unpublished.Semesters.Last().GradedGroups.Should().Be(0);
            unpublished.Semesters.Last().AverageScore.Should().BeNull();
            unpublished.Semesters.Last().AverageScoreChange.Should().BeNull();
            unpublished.Semesters.First().AverageScore.Should().Be(6.8m);
        }
        finally
        {
            await CleanupTrendWorldAsync(world);
        }
    }

    private async Task<ProjectDataTrendsResponse> GetTrends(HttpClient client, User user, string role, string query)
    {
        var response = await Send(client, user, role, HttpMethod.Get, $"/api/project-data/trends?{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectDataTrendsResponse>>();
        return body!.Data!;
    }

    internal sealed class TrendWorld
    {
        public string Token { get; init; } = string.Empty;
        public string CourseCode { get; init; } = string.Empty;
        public string OldSemesterCode { get; init; } = string.Empty;
        public string NewSemesterCode { get; init; } = string.Empty;
        public string OldClassCode { get; init; } = string.Empty;
        public string NewClassCode { get; init; } = string.Empty;
        public User Admin { get; init; } = null!;
        public User Lecturer { get; init; } = null!;
        public User Outsider { get; init; } = null!;
        public Guid[] ClassIds { get; init; } = [];
        public Guid OldProjectOne { get; init; }
        public Guid NewTeamClassId { get; init; }
        public Guid NewAssessmentEvaluationId { get; init; }
    }

    private async Task<TrendWorld> SeedTrendWorldAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var token = Guid.NewGuid().ToString("N")[..8];
        var year = 4000 + Random.Shared.Next(0, 4000);

        User NewUser(string label) => new()
        {
            FullName = $"{label} {token}",
            Email = $"pd-trend-{label.ToLowerInvariant().Replace(' ', '-')}-{token}@example.test",
            NormalizedEmail = $"PD-TREND-{label.ToUpperInvariant().Replace(' ', '-')}-{token}@EXAMPLE.TEST",
            PasswordHash = "integration-test-only",
        };
        var admin = NewUser("Admin");
        var lecturer = NewUser("Lecturer");
        var outsider = NewUser("Outsider lecturer");
        context.Users.AddRange(admin, lecturer, outsider);

        var oldSemester = new Semester { Code = $"TO{token}", Name = $"Trend old {token}", Term = SemesterTerm.Spring, Year = year, Status = SemesterStatus.Planned };
        var newSemester = new Semester { Code = $"TN{token}", Name = $"Trend new {token}", Term = SemesterTerm.Fall, Year = year, Status = SemesterStatus.Planned };
        context.Semesters.AddRange(oldSemester, newSemester);

        var course = new Course { Code = $"TR{token}".ToUpperInvariant(), Name = "Trend course", Status = CourseStatus.Active };
        context.Courses.Add(course);

        // Course score = 40% checkpoint 1 + 40% checkpoint 2 + 20% assessment.
        Rubric NewRubric(string name, Checkpoint? checkpoint, decimal courseWeight) => new()
        {
            Course = course, Checkpoint = checkpoint, Name = name, TotalWeight = 100, CourseWeight = courseWeight,
            Status = RubricStatus.Active, CreatedById = admin.Id,
        };
        var checkpointOne = new Checkpoint { Course = course, Name = "Trend CP1", CheckpointNumber = 1, CourseWeight = 40, Status = CheckpointStatus.Open, CreatedById = admin.Id };
        var checkpointTwo = new Checkpoint { Course = course, Name = "Trend CP2", CheckpointNumber = 2, CourseWeight = 40, Status = CheckpointStatus.Open, CreatedById = admin.Id };
        var rubricOne = NewRubric($"Trend rubric 1 {token}", checkpointOne, 0);
        var rubricTwo = NewRubric($"Trend rubric 2 {token}", checkpointTwo, 0);
        var assessment = NewRubric($"Trend assessment {token}", null, 20);
        context.Rubrics.AddRange(rubricOne, rubricTwo, assessment);

        const string schedule = "[{\"dayOfWeek\":1,\"startTime\":\"07:30\",\"endTime\":\"09:30\"}]";
        Class NewClass(string suffix, Semester semester) => new()
        {
            ClassCode = $"TR{token}-{suffix}", Slug = $"tr-{token}-{suffix.ToLowerInvariant()}", ClassIndex = 1,
            Semester = semester, Course = course, PrimaryLecturerId = lecturer.Id, Status = ClassStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow.AddDays(-2), CompletionReason = "Integration test completion", ScheduleJson = schedule,
        };
        var oldClass = NewClass("OLD", oldSemester);
        var newClass = NewClass("NEW", newSemester);
        context.Classes.AddRange(oldClass, newClass);

        var teamNumber = 0;
        Project NewProject(Class owner, bool potential = false, bool funded = false, bool externallyFunded = false)
        {
            var number = ++teamNumber;
            var team = new Team { Class = owner, TeamCode = $"TT{token}{number}", TeamName = $"Trend team {token} {number}", Status = TeamStatus.Active };
            return new Project { Team = team, Name = $"TrendP {token} {number}", Status = ProjectStatus.Approved, IsHighPotential = potential, IsInternallyFunded = funded, IsExternallyFunded = externallyFunded };
        }
        var t1 = NewProject(oldClass, potential: true);
        var t2 = NewProject(oldClass);
        var t3 = NewProject(newClass, potential: true, funded: true);
        var t4 = NewProject(newClass);
        context.Projects.AddRange(t1, t2, t3, t4);

        Evaluation Eval(Project project, Rubric rubric, EvaluationStatus status, decimal score) => new()
        {
            Project = project, Rubric = rubric, EvaluatorId = lecturer.Id, TotalScore = score, MaxTotalScore = 10, Status = status,
            PublishedAt = status == EvaluationStatus.Published ? DateTime.UtcNow : null,
        };
        void Grade(Project project, decimal one, decimal two, decimal third)
        {
            context.Evaluations.AddRange(
                Eval(project, rubricOne, EvaluationStatus.Published, one),
                Eval(project, rubricTwo, EvaluationStatus.Published, two),
                Eval(project, assessment, EvaluationStatus.Published, third));
        }
        Grade(t1, 8, 6, 10);   // 3.2 + 2.4 + 2.0 = 7.6
        Grade(t2, 6, 6, 6);    // 6.0
        var t3Assessment = Eval(t3, assessment, EvaluationStatus.Published, 9);
        context.Evaluations.AddRange(
            Eval(t3, rubricOne, EvaluationStatus.Published, 9),
            Eval(t3, rubricTwo, EvaluationStatus.Published, 9),
            t3Assessment);                                                      // 9.0
        context.Evaluations.AddRange(
            Eval(t4, rubricOne, EvaluationStatus.Published, 10),
            Eval(t4, rubricTwo, EvaluationStatus.Draft, 10),                    // never official
            Eval(t4, assessment, EvaluationStatus.Submitted, 10));              // not published yet

        await context.SaveChangesAsync();

        return new TrendWorld
        {
            Token = token,
            CourseCode = course.Code,
            OldSemesterCode = oldSemester.Code,
            NewSemesterCode = newSemester.Code,
            OldClassCode = oldClass.ClassCode,
            NewClassCode = newClass.ClassCode,
            Admin = admin,
            Lecturer = lecturer,
            Outsider = outsider,
            ClassIds = [oldClass.Id, newClass.Id],
            OldProjectOne = t1.Id,
            NewTeamClassId = newClass.Id,
            NewAssessmentEvaluationId = t3Assessment.Id,
        };
    }

    /// <summary>What team continuation does: a new project in the newer semester that carries the old labels over.</summary>
    private async Task ContinueProjectAsync(TrendWorld world)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var source = await context.Projects.AsNoTracking().SingleAsync(project => project.Id == world.OldProjectOne);
        var team = new Team
        {
            ClassId = world.NewTeamClassId, TeamCode = $"TC{world.Token}", TeamName = $"Trend continued {world.Token}",
            Status = TeamStatus.Active,
        };
        context.Projects.Add(new Project
        {
            Team = team, Name = $"TrendP {world.Token} continued", Status = ProjectStatus.Approved,
            PreviousProjectId = source.Id, ProjectLineageId = source.ProjectLineageId,
            IsHighPotential = source.IsHighPotential, IsInternallyFunded = source.IsInternallyFunded, IsExternallyFunded = source.IsExternallyFunded, IsAwarded = source.IsAwarded,
        });
        await context.SaveChangesAsync();
    }

    private async Task SetEvaluationStatusAsync(Guid evaluationId, EvaluationStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Evaluations.Where(item => item.Id == evaluationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, status)
                .SetProperty(item => item.PublishedAt, (DateTime?)null));
    }

    private async Task CleanupTrendWorldAsync(TrendWorld world)
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
