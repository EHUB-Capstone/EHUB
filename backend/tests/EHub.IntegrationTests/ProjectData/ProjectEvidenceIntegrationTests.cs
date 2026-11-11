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
    public async Task Evidence_RejectsAnonymousAndNonStaffRoles()
    {
        using var client = factory.CreateClient();
        var url = $"/api/project-data/{Guid.NewGuid()}/evidence";
        (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = new User { FullName = "No staff", Email = "pd-evidence-nostaff@example.test" };
        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor })
        {
            (await Send(client, user, role, HttpMethod.Get, url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Evidence_LecturersOnlySeeProjectsOfClassesTheyTeach()
    {
        var world = await SeedAsync();
        try
        {
            using var client = factory.CreateClient();

            (await GetEvidenceResponse(client, world.Admin, SystemRoles.Admin, world.ProjectA)).StatusCode
                .Should().Be(HttpStatusCode.OK);
            (await GetEvidenceResponse(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectA)).StatusCode
                .Should().Be(HttpStatusCode.OK);
            (await GetEvidenceResponse(client, world.CoLecturer, SystemRoles.Lecturer, world.ProjectA)).StatusCode
                .Should().Be(HttpStatusCode.OK, "an assigned co-lecturer teaches the class too");

            // A project in the scope of the feature, but of somebody else's class: forbidden, not hidden.
            (await GetEvidenceResponse(client, world.Outsider, SystemRoles.Lecturer, world.ProjectA)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await GetEvidenceResponse(client, world.Lecturer, SystemRoles.Lecturer, world.ProjectOutsider)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden);
            (await GetEvidenceResponse(client, world.Outsider, SystemRoles.Lecturer, world.ProjectOutsider)).StatusCode
                .Should().Be(HttpStatusCode.OK);

            (await GetEvidenceResponse(client, world.Admin, SystemRoles.Admin, Guid.NewGuid())).StatusCode
                .Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Evidence_ShowsMentorHistoryOfTheTeamWithoutContactDetails()
    {
        var world = await SeedAsync();
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var teamId = await context.Projects.AsNoTracking()
                    .Where(project => project.Id == world.ProjectA)
                    .Select(project => project.TeamId)
                    .SingleAsync();
                var assignmentId = await context.MentorAssignments.AsNoTracking()
                    .Where(item => item.TeamId == teamId && item.Slot == MentorType.Enterprise)
                    .Select(item => item.Id)
                    .SingleAsync();
                var now = DateTimeOffset.UtcNow;
                context.MentoringSessions.AddRange(
                    Session(assignmentId, MentoringSessionStatus.Completed, now.AddDays(-9)),
                    Session(assignmentId, MentoringSessionStatus.Completed, now.AddDays(-2)),
                    Session(assignmentId, MentoringSessionStatus.Scheduled, now.AddDays(3)),
                    Session(assignmentId, MentoringSessionStatus.Cancelled, now.AddDays(-5)));
                await context.SaveChangesAsync();
            }

            using var client = factory.CreateClient();

            var current = await GetEvidence(client, world.Admin, SystemRoles.Admin, world.ProjectA);
            current.Mentoring.Mentors.Select(item => (item.FullName, item.Slot, item.IsCurrent, item.CompletedSessions))
                .Should().Equal(
                    ($"Mentor ent {world.Token}", "Enterprise", true, 2),
                    ($"Mentor acad {world.Token}", "Academic", true, 0));
            current.Mentoring.TotalCompletedSessions.Should().Be(2);
            current.Mentoring.LastSessionAtUtc.Should().NotBeNull();

            // Class B was completed: the mentor closed with the class is current, the one removed earlier is history,
            // and pending or cancelled assignments are not mentoring history at all.
            var completed = await GetEvidence(client, world.Admin, SystemRoles.Admin, world.ProjectB);
            completed.Mentoring.Mentors.Select(item => (item.FullName, item.IsCurrent)).Should().Equal(
                ($"Historical mentor {world.Token}", true),
                ($"Early-ended mentor {world.Token}", false));

            var raw = await (await GetEvidenceResponse(client, world.Admin, SystemRoles.Admin, world.ProjectA))
                .Content.ReadAsStringAsync();
            raw.Should().NotContain("@example.test", "mentor e-mail addresses are never sent");
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    [Fact]
    public async Task Evidence_ScoreMatchesTheAnalyticsAndKeepsUnpublishedGradesPrivate()
    {
        var world = await SeedAsync();
        try
        {
            await GradeAsync(world, publishSecondCheckpoint: false);
            using var client = factory.CreateClient();

            // The second checkpoint is submitted but not published, so there is no course score and no secret feedback.
            var open = await GetEvidence(client, world.Admin, SystemRoles.Admin, world.ProjectA);
            open.Evaluation.CourseScore.Should().BeNull();
            open.Evaluation.Components.Select(item => (item.Name, item.Status, item.Score, item.WeightPercent)).Should().Equal(
                ("Evidence CP1", ProjectEvidenceStatuses.Published, 8m, 60m),
                ("Evidence CP2", ProjectEvidenceStatuses.PendingPublication, null, 40m));
            open.Evaluation.Components.First().Feedback.Should().Be("Clear problem and users.");
            open.Evaluation.Components.Last().Feedback.Should().BeNull();

            // Another project of the same course has no grade yet.
            var ungraded = await GetEvidence(client, world.Admin, SystemRoles.Admin, world.ProjectB);
            ungraded.Evaluation.Components.Should().OnlyContain(item => item.Status == ProjectEvidenceStatuses.NotGraded);

            await PublishSecondCheckpointAsync(world);

            // 8 * 60% + 6 * 40% = 7.2 - the same number the trends report for the only graded project.
            var published = await GetEvidence(client, world.Admin, SystemRoles.Admin, world.ProjectA);
            published.Evaluation.CourseScore.Should().Be(7.2m);
            var trends = await GetTrends(client, world.Admin, SystemRoles.Admin, $"search={world.Token}");
            trends.Semesters.Single().AverageScore.Should().Be(published.Evaluation.CourseScore);
        }
        finally
        {
            await CleanupAsync(world);
        }
    }

    private async Task<HttpResponseMessage> GetEvidenceResponse(HttpClient client, User user, string role, Guid projectId) =>
        await Send(client, user, role, HttpMethod.Get, $"/api/project-data/{projectId}/evidence");

    private async Task<ProjectEvidenceResponse> GetEvidence(HttpClient client, User user, string role, Guid projectId)
    {
        var response = await GetEvidenceResponse(client, user, role, projectId);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProjectEvidenceResponse>>();
        return body!.Data!;
    }

    private static MentoringSession Session(Guid assignmentId, MentoringSessionStatus status, DateTimeOffset start) => new()
    {
        MentorAssignmentId = assignmentId,
        Title = "Evidence check-in",
        Status = status,
        StartAt = start,
        EndAt = start.AddHours(1),
    };

    /// <summary>Gives the course two weighted checkpoints (60% and 40%) and grades project A on both.</summary>
    private async Task GradeAsync(World world, bool publishSecondCheckpoint)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = await context.Courses.SingleAsync(item => item.Code == world.CourseCode);

        Rubric NewRubric(string name, Checkpoint checkpoint) => new()
        {
            Course = course, Checkpoint = checkpoint, Name = $"{name} {world.Token}", TotalWeight = 100, CourseWeight = 0,
            Status = RubricStatus.Active, CreatedById = world.Admin.Id,
        };
        var first = new Checkpoint { Course = course, Name = "Evidence CP1", CheckpointNumber = 1, CourseWeight = 60, Status = CheckpointStatus.Open, CreatedById = world.Admin.Id };
        var second = new Checkpoint { Course = course, Name = "Evidence CP2", CheckpointNumber = 2, CourseWeight = 40, Status = CheckpointStatus.Open, CreatedById = world.Admin.Id };
        var firstRubric = NewRubric("Evidence rubric 1", first);
        var secondRubric = NewRubric("Evidence rubric 2", second);
        context.Rubrics.AddRange(firstRubric, secondRubric);

        Evaluation Eval(Rubric rubric, EvaluationStatus status, decimal score, string feedback) => new()
        {
            ProjectId = world.ProjectA, Rubric = rubric, EvaluatorId = world.Lecturer.Id, TotalScore = score, MaxTotalScore = 10,
            Status = status, OverallFeedback = feedback,
            PublishedAt = status == EvaluationStatus.Published ? DateTime.UtcNow : null,
        };
        context.Evaluations.AddRange(
            Eval(firstRubric, EvaluationStatus.Published, 8m, "Clear problem and users."),
            Eval(secondRubric, publishSecondCheckpoint ? EvaluationStatus.Published : EvaluationStatus.Submitted, 6m, "Secret until published."));
        await context.SaveChangesAsync();
    }

    private async Task PublishSecondCheckpointAsync(World world)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Evaluations
            .Where(item => item.ProjectId == world.ProjectA && item.Status == EvaluationStatus.Submitted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, EvaluationStatus.Published)
                .SetProperty(item => item.PublishedAt, DateTime.UtcNow));
    }
}
