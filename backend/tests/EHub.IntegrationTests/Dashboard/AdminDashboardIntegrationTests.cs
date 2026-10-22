using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Dashboard;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Dashboard;

[Collection("Sequential")]
public sealed class AdminDashboardIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task GetAdminDashboard_RequiresAdminRole()
    {
        using var client = factory.CreateClient();
        using var anonymousResponse = await client.GetAsync("/api/dashboard/admin");
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var lecturerRequest = AuthorizedRequest(new User
        {
            FullName = "Admin dashboard lecturer",
            Email = "admin-dashboard-lecturer@example.test"
        }, SystemRoles.Lecturer);
        using var lecturerResponse = await client.SendAsync(lecturerRequest);
        lecturerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAdminDashboard_RanksTeamsByIdWhenNamesRepeatAcrossSemesters()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var classCode = $"RANK-{suffix}";
        var teamName = $"Ranking Team {suffix}";
        User lecturer;
        Semester otherSemester;
        Course course;
        Class activeClass;
        Class otherClass;
        Team activeTeam;
        Team otherTeam;
        Project activeProject;
        Project otherProject;
        Checkpoint checkpoint;
        Rubric rubric;
        Submission activeSubmission;
        Submission otherSubmission;
        Evaluation activeEvaluation;
        Evaluation otherEvaluation;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activeSemesterId = await context.Semesters
                .Where(semester => semester.Status == SemesterStatus.Active)
                .Select(semester => semester.Id)
                .FirstAsync();
            lecturer = new User
            {
                FullName = "Ranking evaluator",
                Email = $"ranking-{suffix}@example.test",
                NormalizedEmail = $"RANKING-{suffix}@EXAMPLE.TEST",
                PasswordHash = "integration-test-only"
            };
            otherSemester = new Semester
            {
                Code = $"RK{suffix}",
                Name = $"Ranking {suffix}",
                Term = SemesterTerm.Spring,
                Year = 2072,
                Status = SemesterStatus.Planned
            };
            course = new Course
            {
                Code = $"RANK-{suffix}",
                Name = "Ranking integration course",
                Status = CourseStatus.Active
            };
            // Same class code in two semesters is allowed; teams and projects share names too.
            activeClass = new Class
            {
                ClassCode = classCode,
                Slug = $"rank-{suffix}-active",
                ClassIndex = 1,
                SemesterId = activeSemesterId,
                Course = course,
                PrimaryLecturerId = lecturer.Id,
                Status = ClassStatus.Draft
            };
            otherClass = new Class
            {
                ClassCode = classCode,
                Slug = $"rank-{suffix}-other",
                ClassIndex = 1,
                Semester = otherSemester,
                Course = course,
                PrimaryLecturerId = lecturer.Id,
                Status = ClassStatus.Draft
            };
            activeTeam = new Team { Class = activeClass, TeamCode = $"RA-{suffix}", TeamName = teamName, Status = TeamStatus.Active };
            otherTeam = new Team { Class = otherClass, TeamCode = $"RO-{suffix}", TeamName = teamName, Status = TeamStatus.Active };
            activeProject = new Project { Team = activeTeam, Name = "Ranking Startup", Status = ProjectStatus.Approved };
            otherProject = new Project { Team = otherTeam, Name = "Ranking Startup", Status = ProjectStatus.Approved };
            checkpoint = new Checkpoint
            {
                Course = course,
                Name = "Ranking checkpoint",
                CheckpointNumber = 1,
                CourseWeight = 100m,
                Status = CheckpointStatus.Open
            };
            rubric = new Rubric
            {
                Course = course,
                Checkpoint = checkpoint,
                Name = "Ranking rubric",
                TotalWeight = 100m,
                CourseWeight = 100m,
                Status = RubricStatus.Active
            };
            var submittedAt = DateTime.UtcNow.AddDays(-1);
            activeSubmission = NewSubmission(activeProject, activeTeam, checkpoint, submittedAt);
            otherSubmission = NewSubmission(otherProject, otherTeam, checkpoint, submittedAt);
            activeEvaluation = NewEvaluation(activeProject, activeSubmission, rubric, lecturer, 10m, submittedAt);
            otherEvaluation = NewEvaluation(otherProject, otherSubmission, rubric, lecturer, 9.8m, submittedAt);
            context.AddRange(
                lecturer,
                otherSemester,
                activeClass,
                otherClass,
                activeTeam,
                otherTeam,
                activeProject,
                otherProject,
                checkpoint,
                rubric,
                activeSubmission,
                otherSubmission,
                activeEvaluation,
                otherEvaluation);
            await context.SaveChangesAsync();
        }

        try
        {
            using var client = factory.CreateClient();
            using var request = AuthorizedRequest(lecturer, SystemRoles.Admin);
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminDashboardResponse>>();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotBeNull();
            body!.Data.Should().NotBeNull();
            body.Data!.TopTeams.Should().HaveCountLessThanOrEqualTo(10);
            body.Data.TopTeams.Select(item => item.AvgScore).Should().BeInDescendingOrder();
            body.Data.TopTeams
                .Where(item => item.Team.Name == teamName)
                .Should().SatisfyRespectively(
                    first =>
                    {
                        first.AvgScore.Should().Be(10m);
                        first.Team.ClassId.ClassCode.Should().Be(classCode);
                        first.StartupName.Should().Be("Ranking Startup");
                    },
                    second =>
                    {
                        second.AvgScore.Should().Be(9.8m);
                        second.Team.ClassId.ClassCode.Should().Be(classCode);
                    });
        }
        finally
        {
            using var cleanupScope = factory.Services.CreateScope();
            var cleanupContext = cleanupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await cleanupContext.Evaluations
                .Where(item => item.Id == activeEvaluation.Id || item.Id == otherEvaluation.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Submissions
                .Where(item => item.Id == activeSubmission.Id || item.Id == otherSubmission.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Rubrics.Where(item => item.Id == rubric.Id).ExecuteDeleteAsync();
            await cleanupContext.Checkpoints.Where(item => item.Id == checkpoint.Id).ExecuteDeleteAsync();
            await cleanupContext.Projects
                .Where(item => item.Id == activeProject.Id || item.Id == otherProject.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Teams
                .Where(item => item.Id == activeTeam.Id || item.Id == otherTeam.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Classes
                .Where(@class => @class.Id == activeClass.Id || @class.Id == otherClass.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Courses.Where(item => item.Id == course.Id).ExecuteDeleteAsync();
            await cleanupContext.Semesters.Where(item => item.Id == otherSemester.Id).ExecuteDeleteAsync();
            await cleanupContext.Users.Where(user => user.Id == lecturer.Id).ExecuteDeleteAsync();
        }
    }

    private static Submission NewSubmission(Project project, Team team, Checkpoint checkpoint, DateTime submittedAt) => new()
    {
        Project = project,
        Team = team,
        Checkpoint = checkpoint,
        Title = "Ranking submission",
        Status = SubmissionStatus.Submitted,
        SubmittedAt = submittedAt,
        VersionNumber = 1
    };

    private static Evaluation NewEvaluation(
        Project project,
        Submission submission,
        Rubric rubric,
        User evaluator,
        decimal totalScore,
        DateTime submittedAt) => new()
    {
        Project = project,
        Submission = submission,
        Rubric = rubric,
        Evaluator = evaluator,
        EvaluatorRole = EvaluatorRole.Lecturer,
        TotalScore = totalScore,
        MaxTotalScore = 10m,
        Status = EvaluationStatus.Submitted,
        SubmittedAt = submittedAt.AddHours(1)
    };

    private HttpRequestMessage AuthorizedRequest(User user, string role)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(user, [role]).Token;
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/admin");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
