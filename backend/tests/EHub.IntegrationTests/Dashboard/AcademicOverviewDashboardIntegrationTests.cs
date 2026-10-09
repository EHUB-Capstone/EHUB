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
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Dashboard;

[Collection("Sequential")]
public sealed class AcademicOverviewDashboardIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task GetAcademicOverview_RequiresLecturerRole()
    {
        using var client = factory.CreateClient();
        using var anonymousResponse = await client.GetAsync("/api/dashboard/academic-overview");
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var adminRequest = AuthorizedRequest(new User
        {
            FullName = "Dashboard admin",
            Email = "dashboard-admin@example.test"
        }, SystemRoles.Admin);
        using var adminResponse = await client.SendAsync(adminRequest);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAcademicOverview_ReturnsEmptyStateForLecturerWithoutAssignedClasses()
    {
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest(new User
        {
            FullName = "Unassigned lecturer",
            Email = "unassigned-dashboard-lecturer@example.test"
        }, SystemRoles.Lecturer);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AcademicOverviewResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.HasAssignedClasses.Should().BeFalse();
        body.Data.HasMatchingClasses.Should().BeFalse();
        body.Data!.HasClasses.Should().BeFalse();
        body.Data.Scope.SemesterId.Should().NotBeEmpty();
        body.Data.FilterOptions.Semesters.Should().ContainSingle(item => item.IsActive);
        body.Data.FilterOptions.Subjects.Should().BeEmpty();
        body.Data.FilterOptions.Classes.Should().BeEmpty();
        body.Data.Metrics.Should().BeEquivalentTo(new AcademicOverviewMetricsResponse());
        body.Data.Attention.Should().BeEquivalentTo(new AcademicOverviewAttentionResponse());
        body.Data.ActivityTrend.Should().HaveCount(8);
        body.Data.Classes.Should().BeEmpty();
        body.Data.CheckpointProgress.Should().BeEmpty();
        body.Data.TopTeams.Should().BeEmpty();
        body.Data.LastUpdatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetAcademicOverview_RejectsClassOutsideLecturerScope()
    {
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest(
            new User
            {
                FullName = "Unauthorized dashboard lecturer",
                Email = "unauthorized-dashboard-lecturer@example.test"
            },
            SystemRoles.Lecturer,
            $"/api/dashboard/academic-overview?classId={Guid.NewGuid()}");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AcademicOverviewResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().NotBeNull();
        body!.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task GetAcademicOverview_UsesActiveSemesterAndAssignedLecturerScope()
    {
        User lecturer;
        Guid activeSemesterId;
        Class assignedClass;
        Class otherAssignedClass;
        Course course;
        Course otherCourse;
        Team team;
        Team missedDeadlineTeam;
        Project project;
        Checkpoint checkpoint;
        Checkpoint nearestCheckpoint;
        Checkpoint distantCheckpoint;
        Rubric rubric;
        Submission firstSubmission;
        Submission secondSubmission;
        Evaluation evaluation;
        ClassCheckpointSchedule schedule;
        ClassCheckpointSchedule nearestSchedule;
        ClassCheckpointSchedule distantSchedule;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            activeSemesterId = await context.Semesters
                .Where(semester => semester.Status == SemesterStatus.Active)
                .Select(semester => semester.Id)
                .FirstAsync();
            lecturer = new User
            {
                FullName = "Assigned dashboard lecturer",
                Email = $"assigned-dashboard-{Guid.NewGuid():N}@example.test",
                NormalizedEmail = $"ASSIGNED-DASHBOARD-{Guid.NewGuid():N}@EXAMPLE.TEST",
                PasswordHash = "integration-test-only"
            };
            context.Users.Add(lecturer);
            var suffix = Guid.NewGuid().ToString("N")[..8];
            course = new Course
            {
                Code = $"DASH-{suffix}",
                Name = "Dashboard integration course",
                Status = CourseStatus.Active
            };
            otherCourse = new Course
            {
                Code = $"DASH-OTHER-{suffix}",
                Name = "Other dashboard integration course",
                Status = CourseStatus.Active
            };
            assignedClass = new Class
            {
                ClassCode = $"DASH-{suffix}-01",
                Slug = $"dash-{suffix}-01",
                ClassIndex = 1,
                SemesterId = activeSemesterId,
                Course = course,
                PrimaryLecturerId = lecturer.Id,
                Status = ClassStatus.Draft
            };
            otherAssignedClass = new Class
            {
                ClassCode = $"DASH-OTHER-{suffix}-01",
                Slug = $"dash-other-{suffix}-01",
                ClassIndex = 1,
                SemesterId = activeSemesterId,
                Course = otherCourse,
                PrimaryLecturerId = lecturer.Id,
                Status = ClassStatus.Draft
            };
            team = new Team
            {
                Class = assignedClass,
                TeamCode = $"TEAM-{suffix}",
                TeamName = "Dashboard Team",
                Status = TeamStatus.Active
            };
            missedDeadlineTeam = new Team
            {
                Class = assignedClass,
                TeamCode = $"MISSED-{suffix}",
                TeamName = "Missed Deadline Team",
                Status = TeamStatus.Active
            };
            project = new Project
            {
                Team = team,
                Name = "Dashboard Project",
                Status = ProjectStatus.Approved,
                IsHighPotential = true
            };
            checkpoint = new Checkpoint
            {
                Course = course,
                Name = "Dashboard checkpoint",
                CheckpointNumber = 1,
                CourseWeight = 100m,
                Status = CheckpointStatus.Open
            };
            nearestCheckpoint = new Checkpoint
            {
                Course = course,
                Name = "Nearest upcoming checkpoint",
                CheckpointNumber = 2,
                CourseWeight = 0m,
                Status = CheckpointStatus.Open
            };
            distantCheckpoint = new Checkpoint
            {
                Course = course,
                Name = "Distant upcoming checkpoint",
                CheckpointNumber = 3,
                CourseWeight = 0m,
                Status = CheckpointStatus.Open
            };
            rubric = new Rubric
            {
                Course = course,
                Checkpoint = checkpoint,
                Name = "Dashboard rubric",
                TotalWeight = 100m,
                CourseWeight = 100m,
                Status = RubricStatus.Active
            };
            var submittedAt = DateTime.UtcNow.AddDays(-1);
            firstSubmission = new Submission
            {
                Project = project,
                Team = team,
                Checkpoint = checkpoint,
                Title = "Dashboard submission v1",
                Status = SubmissionStatus.Submitted,
                SubmittedAt = submittedAt,
                VersionNumber = 1
            };
            secondSubmission = new Submission
            {
                Project = project,
                Team = team,
                Checkpoint = checkpoint,
                Title = "Dashboard submission v2",
                Status = SubmissionStatus.Submitted,
                SubmittedAt = submittedAt.AddHours(1),
                VersionNumber = 2
            };
            evaluation = new Evaluation
            {
                Project = project,
                Submission = secondSubmission,
                Rubric = rubric,
                Evaluator = lecturer,
                EvaluatorRole = EvaluatorRole.Lecturer,
                TotalScore = 8.5m,
                MaxTotalScore = 10m,
                Status = EvaluationStatus.Submitted,
                SubmittedAt = submittedAt.AddHours(2)
            };
            schedule = new ClassCheckpointSchedule
            {
                Class = assignedClass,
                Checkpoint = checkpoint,
                StartDateUtc = DateTime.UtcNow.AddDays(-2),
                EndDateUtc = DateTime.UtcNow.AddHours(-12)
            };
            nearestSchedule = new ClassCheckpointSchedule
            {
                Class = assignedClass,
                Checkpoint = nearestCheckpoint,
                StartDateUtc = DateTime.UtcNow,
                EndDateUtc = DateTime.UtcNow.AddDays(1)
            };
            distantSchedule = new ClassCheckpointSchedule
            {
                Class = assignedClass,
                Checkpoint = distantCheckpoint,
                StartDateUtc = DateTime.UtcNow,
                EndDateUtc = DateTime.UtcNow.AddDays(5)
            };
            context.AddRange(
                assignedClass,
                otherAssignedClass,
                team,
                missedDeadlineTeam,
                project,
                checkpoint,
                nearestCheckpoint,
                distantCheckpoint,
                rubric,
                firstSubmission,
                secondSubmission,
                evaluation,
                schedule,
                nearestSchedule,
                distantSchedule);
            await context.SaveChangesAsync();
        }

        try
        {
            using var client = factory.CreateClient();
            using var request = AuthorizedRequest(lecturer, SystemRoles.Lecturer);
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<AcademicOverviewResponse>>();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotBeNull();
            body!.Data.Should().NotBeNull();
            body.Data!.Scope.SemesterId.Should().Be(activeSemesterId);
            body.Data.Scope.CourseId.Should().BeNull();
            body.Data.Scope.ClassId.Should().BeNull();
            body.Data.HasAssignedClasses.Should().BeTrue();
            body.Data.HasMatchingClasses.Should().BeTrue();
            body.Data.HasClasses.Should().BeTrue();
            body.Data.FilterOptions.Semesters.Should().Contain(item =>
                item.Id == activeSemesterId && item.IsActive);
            body.Data.FilterOptions.Subjects.Should().HaveCount(2);
            body.Data.FilterOptions.Subjects.Should().Contain(item =>
                item.Id == course.Id && item.Code == course.Code);
            body.Data.FilterOptions.Classes.Should().HaveCount(2);
            body.Data.FilterOptions.Classes.Should().Contain(item =>
                item.Id == assignedClass.Id && item.CourseId == course.Id);
            body.Data.Metrics.Should().BeEquivalentTo(new AcademicOverviewMetricsResponse
            {
                TotalClasses = 2,
                TotalTeams = 2,
                TotalProjects = 1,
                TotalSubmissions = 1,
                TotalEvaluations = 1,
                TotalPotentialProjects = 1
            });
            body.Data.Attention.Should().BeEquivalentTo(new AcademicOverviewAttentionResponse
            {
                MissedDeadlines = 1
            });
            body.Data.Classes.Should().ContainSingle(item => item.ClassId == assignedClass.Id);
            body.Data.ActivityTrend.Should().HaveCount(8);
            body.Data.ActivityTrend.Sum(item => item.Submissions).Should().Be(1);
            body.Data.ActivityTrend.Sum(item => item.Evaluations).Should().Be(1);
            body.Data.CheckpointProgress.Should().ContainSingle(item =>
                item.CheckpointId == nearestCheckpoint.Id &&
                item.ExpectedTeams == 2 &&
                item.SubmittedTeams == 0 &&
                item.EvaluatedProjects == 0 &&
                item.MissedDeadlineTeams == 0);
            body.Data.TopTeams.Should().ContainSingle(item =>
                item.TeamId == team.Id && item.CourseTotal == 8.5m);

            using (var updateScope = factory.Services.CreateScope())
            {
                var updateContext = updateScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pastReference = DateTime.UtcNow;
                await updateContext.ClassCheckpointSchedules
                    .Where(item => item.Id == nearestSchedule.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.StartDateUtc, pastReference.AddDays(-6))
                        .SetProperty(item => item.EndDateUtc, pastReference.AddDays(-5)));
                await updateContext.ClassCheckpointSchedules
                    .Where(item => item.Id == distantSchedule.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.StartDateUtc, pastReference.AddDays(-11))
                        .SetProperty(item => item.EndDateUtc, pastReference.AddDays(-10)));
            }

            using var filteredRequest = AuthorizedRequest(
                lecturer,
                SystemRoles.Lecturer,
                $"/api/dashboard/academic-overview?semesterId={activeSemesterId}&courseId={course.Id}&classId={assignedClass.Id}");
            using var filteredResponse = await client.SendAsync(filteredRequest);
            var filteredBody = await filteredResponse.Content
                .ReadFromJsonAsync<ApiResponse<AcademicOverviewResponse>>();

            filteredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            filteredBody.Should().NotBeNull();
            filteredBody!.Data.Should().NotBeNull();
            filteredBody.Data!.Scope.SemesterId.Should().Be(activeSemesterId);
            filteredBody.Data.Scope.CourseId.Should().Be(course.Id);
            filteredBody.Data.Scope.ClassId.Should().Be(assignedClass.Id);
            filteredBody.Data.Metrics.TotalClasses.Should().Be(1);
            filteredBody.Data.CheckpointProgress.Should().ContainSingle(item =>
                item.CheckpointId == checkpoint.Id &&
                item.SubmittedTeams == 1 &&
                item.EvaluatedProjects == 1 &&
                item.MissedDeadlineTeams == 1);
        }
        finally
        {
            using var cleanupScope = factory.Services.CreateScope();
            var cleanupContext = cleanupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await cleanupContext.Evaluations
                .Where(item => item.Id == evaluation.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Submissions
                .Where(item => item.Id == firstSubmission.Id || item.Id == secondSubmission.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.ClassCheckpointSchedules
                .Where(item =>
                    item.Id == schedule.Id ||
                    item.Id == nearestSchedule.Id ||
                    item.Id == distantSchedule.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Rubrics
                .Where(item => item.Id == rubric.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Checkpoints
                .Where(item =>
                    item.Id == checkpoint.Id ||
                    item.Id == nearestCheckpoint.Id ||
                    item.Id == distantCheckpoint.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Projects
                .Where(item => item.Id == project.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Teams
                .Where(item => item.Id == team.Id || item.Id == missedDeadlineTeam.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Classes
                .Where(@class => @class.Id == assignedClass.Id || @class.Id == otherAssignedClass.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Courses
                .Where(item => item.Id == course.Id || item.Id == otherCourse.Id)
                .ExecuteDeleteAsync();
            await cleanupContext.Users
                .Where(user => user.Id == lecturer.Id)
                .ExecuteDeleteAsync();
        }
    }

    private HttpRequestMessage AuthorizedRequest(
        User user,
        string role,
        string url = "/api/dashboard/academic-overview")
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(user, [role]).Token;
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
