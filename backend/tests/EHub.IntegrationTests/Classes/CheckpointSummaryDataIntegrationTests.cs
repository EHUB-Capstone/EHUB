using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Classes;

// What happens to AI summaries when the data they came from goes away, and what reviewers say about them.
public sealed partial class TeamWorkflowIntegrationTests
{
    [Fact]
    public async Task DeletingAFile_ErasesTheSummaryTextMadeFromIt()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var kept = await fixture.UploadStoredFileAsync("kept.docx", BuildDocx(CompetitorText));
        var removed = await fixture.UploadStoredFileAsync("removed.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        (await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(kept.Id, removed.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer))
            .IsSuccess.Should().BeTrue();
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, new ScriptedSummarizer(_ => Result.Success(Answer()))), fixture.Clock.UtcNow);
        (await LoadFileSummaryAsync(fixture, removed.Id)).ContentJson.Should().Contain("nilon", "the summary quotes the file before it is deleted");

        // The student who uploaded it deletes the file while the checkpoint is open.
        (await fixture.CreateFileHandler().DeleteAsync(teamId, 1, removed.Id, fixture.Seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();

        fixture.Context.ChangeTracker.Clear();
        var row = await fixture.Context.SubmissionFileSummaries.IgnoreQueryFilters().SingleAsync(item => item.SubmissionFileId == removed.Id);
        row.IsDeleted.Should().BeTrue();
        row.ContentJson.Should().BeNull("no text taken from the deleted file may be left behind");
        row.PreviewJson.Should().BeNull();
        var read = await CreateSummaryHandler(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        read.Value.Files.Should().ContainSingle().Which.FileId.Should().Be(kept.Id);
        (await LoadFileSummaryAsync(fixture, kept.Id)).ContentJson.Should().NotBeNull("other files keep their summary");
    }

    [Fact]
    public async Task DeletingAFileThatIsStillQueued_CancelsItsSummaryAndTheWorkerSkipsIt()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var queued = await fixture.UploadStoredFileAsync("queued.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        (await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(queued.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();

        (await fixture.CreateFileHandler().DeleteAsync(teamId, 1, queued.Id, fixture.Seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();

        var summarizer = new ScriptedSummarizer(_ => Result.Success(Answer()));
        (await DrainSummariesAsync(CreateSummaryGenerator(fixture, summarizer), fixture.Clock.UtcNow)).Should().Be(0);
        summarizer.Prompts.Should().BeEmpty("a deleted file is never read or sent to the model");
        fixture.Context.ChangeTracker.Clear();
        var row = await fixture.Context.SubmissionFileSummaries.IgnoreQueryFilters().SingleAsync(item => item.SubmissionFileId == queued.Id);
        row.Status.Should().Be(CheckpointSummaryStatus.Cancelled);
        row.IsDeleted.Should().BeTrue();
    }

    private static SetCheckpointSummaryFeedbackRequest Verdict(string rating, string? reason = null) => new() { Rating = rating, Reason = reason };

    [Fact]
    public async Task SummaryFeedback_IsPerReviewerCanBeChangedAndCleared()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var plan = await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        (await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, new ScriptedSummarizer(_ => Result.Success(Answer()))), fixture.Clock.UtcNow);
        var feedback = new CheckpointSummaryFeedbackHandler(fixture.Context, fixture.Clock, EnabledSummaries);
        // A second reviewer: an administrator is allowed on every team (a seeded user stands in for their account).
        var admin = fixture.Seed.ProposerUserId;
        async Task<WorkspaceSummaryFeedback?> Mine(Guid userId, string role) =>
            FileOf((await CreateSummaryHandler(fixture).GetAsync(teamId, 1, userId, role)).Value, plan.Id).MyFeedback;

        (await Mine(fixture.Seed.LecturerId, SystemRoles.Lecturer)).Should().BeNull();

        (await feedback.SetAsync(teamId, 1, plan.Id, Verdict("Helpful"), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        (await Mine(fixture.Seed.LecturerId, SystemRoles.Lecturer))!.Rating.Should().Be("Helpful");
        (await Mine(admin, SystemRoles.Admin)).Should().BeNull("a verdict is private to the reviewer who gave it");

        // Changing the verdict updates the same row; a second reviewer's verdict is a row of its own.
        (await feedback.SetAsync(teamId, 1, plan.Id, Verdict("NotHelpful", "WrongEvidence"), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        (await feedback.SetAsync(teamId, 1, plan.Id, Verdict("Helpful"), admin, SystemRoles.Admin)).IsSuccess.Should().BeTrue();
        var changed = (await Mine(fixture.Seed.LecturerId, SystemRoles.Lecturer))!;
        changed.Rating.Should().Be("NotHelpful");
        changed.Reason.Should().Be("WrongEvidence");
        fixture.Context.ChangeTracker.Clear();
        var rows = await fixture.Context.CheckpointSummaryFeedbacks.AsNoTracking().Where(row => row.UserId == fixture.Seed.LecturerId || row.UserId == admin).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(row => row.PromptVersion == "v13" && row.Model == "scripted-model", "the verdict remembers which prompt and model made the summary");

        // Clearing removes only the caller's verdict, and it can be given again afterwards.
        (await feedback.ClearAsync(teamId, 1, plan.Id, fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        (await Mine(fixture.Seed.LecturerId, SystemRoles.Lecturer)).Should().BeNull();
        (await Mine(admin, SystemRoles.Admin)).Should().NotBeNull();
        (await feedback.SetAsync(teamId, 1, plan.Id, Verdict("Helpful"), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        (await Mine(fixture.Seed.LecturerId, SystemRoles.Lecturer))!.Rating.Should().Be("Helpful");

        // The administrators' diagnostics count the verdicts by prompt version.
        var diagnostics = await new CheckpointSummaryDiagnosticsHandler(fixture.Context, fixture.Clock, EnabledSummaries,
            new ScriptedSummarizer(_ => Result.Success(Answer()))).GetAsync(SystemRoles.Admin);
        diagnostics.Value.FeedbackByPromptVersion.Should().Contain(item => item.PromptVersion == "v13" && item.Helpful >= 2);
    }

    [Fact]
    public async Task SummaryFeedback_RefusesWhatCannotBeRated()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var ready = await fixture.UploadStoredFileAsync("ready.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        (await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(ready.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, new ScriptedSummarizer(_ => Result.Success(Answer()))), fixture.Clock.UtcNow);
        var waiting = await fixture.UploadStoredFileAsync("waiting.docx", BuildDocx(CompetitorText));
        (await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(waiting.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        var feedback = new CheckpointSummaryFeedbackHandler(fixture.Context, fixture.Clock, EnabledSummaries);
        var lecturer = fixture.Seed.LecturerId;

        // Students never rate, and the refusal comes before any lookup.
        (await feedback.SetAsync(teamId, 1, ready.Id, Verdict("Helpful"), fixture.Seed.ProposerUserId, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await feedback.ClearAsync(teamId, 1, ready.Id, fixture.Seed.ProposerUserId, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        // A summary that is not finished, and a file that is not in this checkpoint, are both "not found".
        (await feedback.SetAsync(teamId, 1, waiting.Id, Verdict("Helpful"), lecturer, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceNotFound);
        (await feedback.SetAsync(teamId, 1, Guid.NewGuid(), Verdict("Helpful"), lecturer, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceNotFound);
        // Only the fixed ratings and reasons are accepted, and a reason goes only with NotHelpful.
        foreach (var bad in new[] { Verdict("Great"), Verdict("helpful"), Verdict(""), Verdict("Helpful", "Other"), Verdict("NotHelpful", "Because I said so") })
            (await feedback.SetAsync(teamId, 1, ready.Id, bad, lecturer, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceValidationError);
        (await feedback.SetAsync(teamId, 1, ready.Id, Verdict("NotHelpful", "MissingKeyPoint"), lecturer, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        // Off in this environment: nothing is stored.
        var off = new CheckpointSummaryFeedbackHandler(fixture.Context, fixture.Clock, new CheckpointSummarySettings { Enabled = false });
        (await off.SetAsync(teamId, 1, ready.Id, Verdict("Helpful"), lecturer, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryDisabled);
        (await off.ClearAsync(teamId, 1, ready.Id, lecturer, SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryDisabled);
    }

    [Fact]
    public async Task SummaryFeedbackApi_RequiresAReviewerAndIsOffWhenSummariesAreOff()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var plan = await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        var tokens = fixture.Scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Identity.IJwtTokenService>();
        var user = await fixture.Context.Users.AsNoTracking().SingleAsync(item => item.Id == fixture.Seed.LecturerId);
        using var client = _factory.CreateClient();
        var url = $"/api/workspace/checkpoints/teams/{fixture.Seed.TeamId}/checkpoints/1/ai-summary/files/{plan.Id}/feedback";

        async Task<System.Net.HttpStatusCode> Send(HttpMethod method, string? role)
        {
            using var request = new HttpRequestMessage(method, url);
            if (method == HttpMethod.Put)
                request.Content = new StringContent("{\"rating\":\"Helpful\"}", System.Text.Encoding.UTF8, "application/json");
            if (role is not null)
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(user, [role]).Token);
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete })
        {
            (await Send(method, null)).Should().Be(System.Net.HttpStatusCode.Unauthorized);
            (await Send(method, SystemRoles.Student)).Should().Be(System.Net.HttpStatusCode.Forbidden);
            // The test host keeps the default Summarization:Provider=Disabled.
            (await Send(method, SystemRoles.Lecturer)).Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }
}
