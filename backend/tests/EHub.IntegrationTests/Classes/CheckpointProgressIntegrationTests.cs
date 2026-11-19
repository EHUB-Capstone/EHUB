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

// How a team's checkpoints mention what the rubrics ask for: reviewers only, nothing at all while AI summaries are off.
public sealed partial class TeamWorkflowIntegrationTests
{
    private static CheckpointProgressHandler CreateProgress(UploadFixture fixture, CheckpointSummarySettings? settings = null)
    {
        fixture.Context.ChangeTracker.Clear();
        return new CheckpointProgressHandler(fixture.Context, settings ?? EnabledSummaries);
    }

    private static WorkspaceFileRequirementFinding Finding(params (string Label, bool Found)[] cues) => new()
    {
        Index = 0, Kind = "Criterion", Title = "Criterion", Finding = "Related",
        Cues = cues.Select(cue => new WorkspaceCriterionCue { Label = cue.Label, Found = cue.Found }).ToArray()
    };

    /// <summary>A file of the team in a checkpoint, with the requirement findings its quick view stored (none when the file was not read).</summary>
    private static async Task AddFileWithFindingsAsync(
        UploadFixture fixture, int checkpointNumber, string fileName, WorkspaceFileRequirementFinding[]? findings, int version = 1)
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
        if (findings is not null)
        {
            context.SubmissionFileSummaries.Add(new SubmissionFileSummary
            {
                SubmissionFile = file, TeamId = teamId, CheckpointId = checkpoint.Id, FileVersionNumber = version, Status = CheckpointSummaryStatus.Pending,
                PreviewJson = JsonSerializer.Serialize(new WorkspaceFilePreview { Requirements = findings }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                RequestedById = fixture.Seed.LecturerId, RequestedAtUtc = now, CreatedBy = fixture.Seed.LecturerId
            });
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Progress_ShowsWhatEachCheckpointMentions_AndNamesFilesNobodyRead()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var teamId = fixture.Seed.TeamId!.Value;
        await AddFileWithFindingsAsync(fixture, 1, "plan.pdf", [Finding(("Persona", false), ("Roadmap", true))]);
        await AddFileWithFindingsAsync(fixture, 1, "scan.pdf", findings: null, version: 2);
        await AddFileWithFindingsAsync(fixture, 2, "deck.pdf", [Finding(("Persona", true), ("Roadmap", true))]);

        var result = await CreateProgress(fixture).GetAsync(teamId, 2, fixture.Seed.LecturerId, SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        result.Value.Checkpoints.Select(item => (item.Number, item.FileCount, item.ReadFileCount)).Should().Equal((1, 2, 1), (2, 1, 1));
        result.Value.Concepts.Single(item => item.Label == "Persona").Change.Should().Be("Gained");
        result.Value.Concepts.Single(item => item.Label == "Roadmap").Change.Should().Be("Held");

        // The first checkpoint has nothing earlier to be compared with.
        (await CreateProgress(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer))
            .Value.Concepts.Should().BeEmpty();
    }

    [Fact]
    public async Task Progress_UsesTheNewestVersionOfAFile()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var teamId = fixture.Seed.TeamId!.Value;
        await AddFileWithFindingsAsync(fixture, 1, "plan.pdf", [Finding(("Persona", false))]);
        await AddFileWithFindingsAsync(fixture, 2, "deck.pdf", [Finding(("Persona", false))], version: 1);
        // Version 2 of the checkpoint 2 file mentions the persona; the older version no longer counts.
        await AddFileWithFindingsAsync(fixture, 2, "deck.pdf", [Finding(("Persona", true))], version: 2);

        var result = await CreateProgress(fixture).GetAsync(teamId, 2, fixture.Seed.LecturerId, SystemRoles.Lecturer);

        result.Value.Concepts.Should().ContainSingle().Which.Change.Should().Be("Gained");
    }

    [Fact]
    public async Task Progress_IsForReviewersOfTheTeamOnly_AndAbsentWhileSummariesAreOff()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var teamId = fixture.Seed.TeamId!.Value;
        await AddFileWithFindingsAsync(fixture, 1, "plan.pdf", [Finding(("Persona", false))]);
        await AddFileWithFindingsAsync(fixture, 2, "deck.pdf", [Finding(("Persona", true))]);

        (await CreateProgress(fixture).GetAsync(teamId, 2, fixture.Seed.MentorUserId, SystemRoles.Mentor)).IsSuccess.Should().BeTrue();
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Lecturer, "progress-outsider");
        foreach (var (userId, role) in new[] { (fixture.Seed.ProposerUserId, SystemRoles.Student), (outsider.Id, SystemRoles.Lecturer) })
            (await CreateProgress(fixture).GetAsync(teamId, 2, userId, role)).Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await CreateProgress(fixture, new CheckpointSummarySettings { Enabled = false })
            .GetAsync(teamId, 2, fixture.Seed.LecturerId, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryDisabled);
    }
}
