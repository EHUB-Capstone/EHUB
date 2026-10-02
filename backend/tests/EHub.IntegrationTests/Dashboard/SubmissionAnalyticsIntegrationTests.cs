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
public sealed class SubmissionAnalyticsIntegrationTests(CustomWebApplicationFactory factory)
{
    private const string Endpoint = "/api/dashboard/submission-analytics";

    [Fact]
    public async Task RequiresAuthenticationAndAStaffRole()
    {
        using var client = factory.CreateClient();
        (await client.GetAsync(Endpoint)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var role in new[] { SystemRoles.Student, SystemRoles.Mentor })
        {
            using var request = Request(new User { FullName = "Analytics test", Email = "analytics@example.test" }, role, Endpoint);
            (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task RejectsInvalidFiltersAndResourcesOutsideLecturerScope()
    {
        using var client = factory.CreateClient();
        var user = new User { FullName = "Unassigned analytics lecturer", Email = "unassigned-analytics@example.test" };
        foreach (var filter in new[] { "semester=XX", "year=1999", "checkpointNumber=0", $"classId={Guid.Empty}" })
        {
            using var request = Request(user, SystemRoles.Lecturer, $"{Endpoint}?{filter}");
            (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        foreach (var filter in new[] { $"classId={Guid.NewGuid()}", $"teamId={Guid.NewGuid()}" })
        {
            using var request = Request(user, SystemRoles.Lecturer, $"{Endpoint}?{filter}");
            (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        using var emptyRequest = Request(user, SystemRoles.Lecturer, Endpoint);
        var empty = await client.SendAsync(emptyRequest);
        var body = await empty.Content.ReadFromJsonAsync<ApiResponse<SubmissionAnalyticsResponse>>();
        body!.Data!.ExpectedCount.Should().Be(0);
    }

    [Fact]
    public async Task CountsArtifactsOnceFiltersByScopeAndRecalculatesAfterReopening()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var semester = await db.Semesters.FirstAsync(item => item.Status == SemesterStatus.Active);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var lecturer = new User { FullName = "Analytics lecturer", Email = $"analytics-{suffix}@example.test",
            NormalizedEmail = $"ANALYTICS-{suffix}@EXAMPLE.TEST", PasswordHash = "integration-test-only", Status = UserStatus.Active };
        var course = new Course { Code = $"AN-{suffix}", Name = "Analytics course", Status = CourseStatus.Active };
        var firstClass = new Class { ClassCode = $"AN-{suffix}-1", Slug = $"an-{suffix}-1", Course = course,
            SemesterId = semester.Id, ClassIndex = 1, PrimaryLecturerId = lecturer.Id, Status = ClassStatus.Draft };
        var secondClass = new Class { ClassCode = $"AN-{suffix}-2", Slug = $"an-{suffix}-2", Course = course,
            SemesterId = semester.Id, ClassIndex = 2, Status = ClassStatus.Draft };
        var assignment = new ClassLecturer { Class = secondClass, Lecturer = lecturer };
        var checkpoint = new Checkpoint { Course = course, Name = "Analytics CP1", CheckpointNumber = 1,
            CourseWeight = 50, Status = CheckpointStatus.Open, RequirementsJson = "[\"Required text\"]" };
        var unscheduled = new Checkpoint { Course = course, Name = "Analytics CP2", CheckpointNumber = 2,
            CourseWeight = 50, Status = CheckpointStatus.Open };
        var names = new[] { "File team", "Link team", "Text-only team", "Empty submission team", "No workspace team" };
        var teams = names.Select((name, index) => new Team { Class = firstClass, TeamCode = $"AN-{index}", TeamName = name }).ToArray();
        var otherTeam = new Team { Class = secondClass, TeamCode = "AN-OTHER", TeamName = "Other class team" };
        var projects = teams.Take(4).Select(team => new Project { Team = team, Name = team.TeamName, Status = ProjectStatus.Approved }).ToArray();
        var submittedAt = DateTime.UtcNow.AddDays(-2);
        var fileSubmission = SubmissionFor(teams[0], projects[0], checkpoint, 1, submittedAt);
        var secondFileSubmission = SubmissionFor(teams[0], projects[0], checkpoint, 2, submittedAt.AddHours(1));
        var linkSubmission = SubmissionFor(teams[1], projects[1], checkpoint, 1, submittedAt);
        var textSubmission = SubmissionFor(teams[2], projects[2], checkpoint, 1, null);
        var emptySubmission = SubmissionFor(teams[3], projects[3], checkpoint, 1, submittedAt);
        var schedule = new ClassCheckpointSchedule { Class = firstClass, Checkpoint = checkpoint,
            StartDateUtc = DateTime.UtcNow.AddDays(-3), EndDateUtc = DateTime.UtcNow.AddDays(-1) };
        var secondSchedule = new ClassCheckpointSchedule { Class = secondClass, Checkpoint = checkpoint,
            StartDateUtc = DateTime.UtcNow.AddDays(-3), EndDateUtc = DateTime.UtcNow.AddDays(1) };
        db.AddRange(lecturer, course, firstClass, secondClass, assignment, checkpoint, unscheduled, otherTeam,
            schedule, secondSchedule, fileSubmission, secondFileSubmission, linkSubmission, textSubmission, emptySubmission);
        db.AddRange(teams);
        db.AddRange(projects);
        db.AddRange(FileFor(fileSubmission, submittedAt), FileFor(secondFileSubmission, submittedAt.AddHours(1)),
            new SubmissionLink { Submission = linkSubmission, SubmittedBy = lecturer, Name = "Submission link",
                Url = "https://example.test/submission", SubmittedAt = submittedAt },
            new SubmissionRequirementContent { Submission = textSubmission, RequirementIndex = 0, Content = "Text alone is not submitted" });
        await db.SaveChangesAsync();

        try
        {
            using var client = factory.CreateClient();
            var classFilter = $"{Endpoint}?classId={firstClass.Id}&checkpointNumber=1";
            var first = await GetAsync(client, lecturer, SystemRoles.Lecturer, classFilter);
            first.ExpectedCount.Should().Be(5);
            first.SubmittedCount.Should().Be(2);
            first.NotSubmittedCount.Should().Be(3);
            first.MissingCount.Should().Be(3);
            first.Items.Single(item => item.TeamId == teams[0].Id).SubmittedAtUtc.Should().BeCloseTo(submittedAt, TimeSpan.FromSeconds(1));
            first.Items.Single(item => item.TeamId == teams[4].Id).HasWorkspace.Should().BeFalse();
            first.Items.Single(item => item.TeamId == teams[2].Id).Status.Should().Be("Missing");
            var admin = await GetAsync(client, lecturer, SystemRoles.Admin, classFilter);
            admin.SubmittedCount.Should().Be(2);
            var secondary = await GetAsync(client, lecturer, SystemRoles.Lecturer, $"{Endpoint}?classId={secondClass.Id}&checkpointNumber=1");
            secondary.ExpectedCount.Should().Be(1);
            secondary.NotSubmittedCount.Should().Be(1);
            secondary.MissingCount.Should().Be(0);
            var teamScope = await GetAsync(client, lecturer, SystemRoles.Lecturer, $"{classFilter}&teamId={teams[1].Id}");
            teamScope.ExpectedCount.Should().Be(1);
            teamScope.SubmittedCount.Should().Be(1);
            var noDeadline = await GetAsync(client, lecturer, SystemRoles.Lecturer, $"{Endpoint}?classId={firstClass.Id}&checkpointNumber=2");
            noDeadline.NotSubmittedCount.Should().Be(5);
            noDeadline.MissingCount.Should().Be(0);
            var term = semester.Term switch { SemesterTerm.Spring => "SP", SemesterTerm.Summer => "SU", _ => "FA" };
            var scoped = await GetAsync(client, lecturer, SystemRoles.Lecturer, $"{classFilter}&semester={term}&year={semester.Year}");
            scoped.ExpectedCount.Should().Be(5);
            var otherYear = await GetAsync(client, lecturer, SystemRoles.Lecturer, $"{classFilter}&year={semester.Year + 1}");
            otherYear.ExpectedCount.Should().Be(0);
            schedule.EndDateUtc = DateTime.UtcNow.AddDays(1);
            await db.SaveChangesAsync();
            var reopened = await GetAsync(client, lecturer, SystemRoles.Lecturer, classFilter);
            reopened.MissingCount.Should().Be(0);
            reopened.NotSubmittedCount.Should().Be(3);
        }
        finally
        {
            var teamIds = teams.Select(team => team.Id).Append(otherTeam.Id).ToArray();
            var submissionIds = new[] { fileSubmission.Id, secondFileSubmission.Id, linkSubmission.Id, textSubmission.Id, emptySubmission.Id };
            await db.SubmissionFiles.Where(item => submissionIds.Contains(item.SubmissionId)).ExecuteDeleteAsync();
            await db.SubmissionLinks.Where(item => submissionIds.Contains(item.SubmissionId)).ExecuteDeleteAsync();
            await db.SubmissionRequirementContents.Where(item => submissionIds.Contains(item.SubmissionId)).ExecuteDeleteAsync();
            await db.Submissions.Where(item => submissionIds.Contains(item.Id)).ExecuteDeleteAsync();
            await db.ClassCheckpointSchedules.Where(item => item.ClassId == firstClass.Id || item.ClassId == secondClass.Id).ExecuteDeleteAsync();
            await db.Checkpoints.Where(item => item.CourseId == course.Id).ExecuteDeleteAsync();
            await db.Projects.Where(item => teamIds.Contains(item.TeamId)).ExecuteDeleteAsync();
            await db.Teams.Where(item => teamIds.Contains(item.Id)).ExecuteDeleteAsync();
            await db.ClassLecturers.Where(item => item.ClassId == secondClass.Id).ExecuteDeleteAsync();
            await db.Classes.Where(item => item.Id == firstClass.Id || item.Id == secondClass.Id).ExecuteDeleteAsync();
            await db.Courses.Where(item => item.Id == course.Id).ExecuteDeleteAsync();
            await db.Users.Where(item => item.Id == lecturer.Id).ExecuteDeleteAsync();
        }
    }

    private static Submission SubmissionFor(Team team, Project project, Checkpoint checkpoint, int version, DateTime? submittedAt) =>
        new() { Team = team, Project = project, Checkpoint = checkpoint, Title = "Analytics submission", VersionNumber = version,
            Status = submittedAt.HasValue ? SubmissionStatus.Submitted : SubmissionStatus.Draft, SubmittedAt = submittedAt };

    private static SubmissionFile FileFor(Submission submission, DateTime uploadedAt) => new()
    {
        Submission = submission, FileName = "test.pdf", OriginalName = "test.pdf", FileUrl = "https://example.test/test.pdf",
        CloudinaryPublicId = $"analytics-{submission.Id}", MimeType = "application/pdf", FileSize = 100, UploadedAt = uploadedAt
    };

    private async Task<SubmissionAnalyticsResponse> GetAsync(HttpClient client, User user, string role, string url)
    {
        using var request = Request(user, role, url);
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SubmissionAnalyticsResponse>>();
        return body!.Data!;
    }

    private HttpRequestMessage Request(User user, string role, string url)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [role]).Token;
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
