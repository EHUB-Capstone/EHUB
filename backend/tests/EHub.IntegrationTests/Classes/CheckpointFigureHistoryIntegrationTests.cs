using System.Text.Json;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EHub.IntegrationTests.Classes;

// Figures of a checkpoint against the team's earlier checkpoints: reviewers only, nothing at all while AI summaries are off.
public sealed partial class TeamWorkflowIntegrationTests
{
    private static CheckpointFigureHistoryHandler CreateFigureHistory(UploadFixture fixture, CheckpointSummarySettings? settings = null)
    {
        fixture.Context.ChangeTracker.Clear();
        return new CheckpointFigureHistoryHandler(fixture.Context, settings ?? EnabledSummaries);
    }

    /// <summary>A file of the team in a checkpoint of the course, with the figures the quick view took from it.</summary>
    private static async Task AddFileWithFiguresAsync(
        UploadFixture fixture, int checkpointNumber, string fileName, string[]? figures, int version = 1)
    {
        var context = fixture.Context;
        context.ChangeTracker.Clear();
        var teamId = fixture.Seed.TeamId!.Value;
        var courseId = await context.Classes.Where(item => item.Id == fixture.Seed.ClassId).Select(item => item.CourseId).SingleAsync();
        var checkpoint = await context.Checkpoints.FirstOrDefaultAsync(item => item.CourseId == courseId && item.ClassId == null && item.CheckpointNumber == checkpointNumber);
        if (checkpoint is null)
        {
            checkpoint = new Checkpoint
            {
                CourseId = courseId, Name = $"Checkpoint {checkpointNumber}", CheckpointNumber = checkpointNumber,
                Status = CheckpointStatus.Draft, CreatedById = fixture.Seed.AdminId
            };
            context.Checkpoints.Add(checkpoint);
            await context.SaveChangesAsync();
        }
        var projectId = await context.Projects.Where(item => item.TeamId == teamId).Select(item => item.Id).FirstAsync();
        var now = fixture.Clock.UtcNow;
        var submission = new Submission
        {
            ProjectId = projectId, TeamId = teamId, CheckpointId = checkpoint.Id, Title = fileName, Status = SubmissionStatus.Submitted,
            SubmittedAt = now, VersionNumber = version, SubmittedById = fixture.Seed.ProposerUserId, CreatedBy = fixture.Seed.ProposerUserId
        };
        var file = new SubmissionFile
        {
            Submission = submission, VersionNumber = version, FileName = fileName, OriginalName = fileName, FileUrl = "https://example.test/" + fileName,
            CloudinaryPublicId = "id-" + Guid.NewGuid(), MimeType = "application/pdf", FileSize = 9, FileType = SubmissionFileType.Report,
            UploadedById = fixture.Seed.ProposerUserId, UploadedAt = now, CreatedBy = fixture.Seed.ProposerUserId
        };
        context.Submissions.Add(submission);
        context.SubmissionFiles.Add(file);
        if (figures is not null)
        {
            context.SubmissionFileSummaries.Add(new SubmissionFileSummary
            {
                SubmissionFile = file, TeamId = teamId, CheckpointId = checkpoint.Id, FileVersionNumber = version, Status = CheckpointSummaryStatus.Pending,
                PreviewJson = JsonSerializer.Serialize(new WorkspaceFilePreview { Figures = figures }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                RequestedById = fixture.Seed.LecturerId, RequestedAtUtc = now, CreatedBy = fixture.Seed.LecturerId
            });
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task FigureHistory_FlagsFiguresThatDisagreeWithAnEarlierCheckpoint_ForReviewersOnly()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var teamId = fixture.Seed.TeamId!.Value;
        await AddFileWithFiguresAsync(fixture, 1, "slides.pdf", ["Quy mô mẫu hợp lệ: N = 51 nhân sự trong lĩnh vực Logistics."]);
        await AddFileWithFiguresAsync(fixture, 2, "report.pdf", ["52 Doanh nghiệp tham gia khảo sát từ Forwarder, 3PL, Express và XNK."]);
        await AddFileWithFiguresAsync(fixture, 1, "appendix.pdf", figures: null, version: 2);

        var result = await CreateFigureHistory(fixture).GetAsync(teamId, 2, fixture.Seed.LecturerId, SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        result.Value.ComparedCheckpoints.Should().Equal(1);
        var conflict = result.Value.Conflicts.Should().ContainSingle().Subject;
        conflict.Values.Select(value => value.Source).Should().BeEquivalentTo("CP1 · slides.pdf", "CP2 · report.pdf");
        conflict.Values.Select(value => value.Value).Should().BeEquivalentTo("51", "52");
        // A file with no figures yet is named instead of being silently treated as consistent.
        result.Value.UncheckedFiles.Should().Equal("CP1 · appendix.pdf");

        // The first checkpoint has nothing earlier to compare with.
        (await CreateFigureHistory(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer))
            .Value.Should().Match<WorkspaceCheckpointFigureHistoryResponse>(item => item.Conflicts.Count == 0 && item.ComparedCheckpoints.Count == 0);

        (await CreateFigureHistory(fixture).GetAsync(teamId, 2, fixture.Seed.MentorUserId, SystemRoles.Mentor)).IsSuccess.Should().BeTrue();
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Lecturer, "history-outsider");
        foreach (var (userId, role) in new[] { (fixture.Seed.ProposerUserId, SystemRoles.Student), (outsider.Id, SystemRoles.Lecturer) })
            (await CreateFigureHistory(fixture).GetAsync(teamId, 2, userId, role)).Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await CreateFigureHistory(fixture, new CheckpointSummarySettings { Enabled = false })
            .GetAsync(teamId, 2, fixture.Seed.LecturerId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryDisabled);
    }

    [Fact]
    public async Task FigureHistory_IgnoresDisagreementsBetweenTwoEarlierCheckpoints_AndUsesTheNewestVersionOfAFile()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var teamId = fixture.Seed.TeamId!.Value;
        // Checkpoints 1 and 2 disagree with each other (51 and 52); checkpoint 3 agrees with 52.
        await AddFileWithFiguresAsync(fixture, 1, "slides.pdf", ["Quy mô mẫu hợp lệ: N = 51 nhân sự trong lĩnh vực Logistics."]);
        await AddFileWithFiguresAsync(fixture, 2, "report.pdf", ["52 Doanh nghiệp tham gia khảo sát từ Forwarder, 3PL, Express và XNK."]);
        // An older version of the checkpoint 3 file said 51; the newest says 52 and is the one that counts.
        await AddFileWithFiguresAsync(fixture, 3, "final.pdf", ["Quy mô mẫu hợp lệ: N = 51 nhân sự trong lĩnh vực Logistics."], version: 1);
        await AddFileWithFiguresAsync(fixture, 3, "final.pdf", ["Quy mô mẫu hợp lệ: N = 52 nhân sự trong lĩnh vực Logistics."], version: 2);

        var result = await CreateFigureHistory(fixture).GetAsync(teamId, 3, fixture.Seed.LecturerId, SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        result.Value.ComparedCheckpoints.Should().Equal(1, 2);
        // Only the checkpoint 1 figure (51) disagrees with the current 52; the 1 versus 2 disagreement is not repeated here.
        var conflict = result.Value.Conflicts.Should().ContainSingle().Subject;
        conflict.Values.Select(value => value.Source).Should().Contain("CP3 · final.pdf").And.Contain("CP1 · slides.pdf");
    }
}
