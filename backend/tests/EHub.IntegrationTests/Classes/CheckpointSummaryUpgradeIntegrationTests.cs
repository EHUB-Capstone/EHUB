using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Domain.Enums;
using EHub.Contracts.Workspaces;
using Microsoft.EntityFrameworkCore;
using EHub.Shared.Errors;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EHub.IntegrationTests.Classes;

public sealed partial class TeamWorkflowIntegrationTests
{
    [Fact]
    public async Task SummaryFeedback_RemainsHistoricalAfterRefreshUntilTheReviewerRatesTheNewResult()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var plan = await fixture.UploadStoredFileAsync("feedback-refresh-demo.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        await SetRequirementsAsync(fixture, "Khảo sát khách hàng");
        var model = new ScriptedSummarizer(_ => Result.Success(Answer()));
        await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer);
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, model), fixture.Clock.UtcNow);
        var original = await CreateSummaryHandler(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        var originalGeneration = FileOf(original.Value, plan.Id).GenerationId;
        originalGeneration.Should().NotBeNull();
        var feedback = new CheckpointSummaryFeedbackHandler(fixture.Context, fixture.Clock, EnabledSummaries);
        (await feedback.SetAsync(teamId, 1, plan.Id, new SetCheckpointSummaryFeedbackRequest { Rating = "Helpful", GenerationId = originalGeneration },
            fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();

        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
        await SetRequirementsAsync(fixture, "Phân tích đối thủ");
        await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer);
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, model), fixture.Clock.UtcNow);
        var refreshed = await CreateSummaryHandler(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        FileOf(refreshed.Value, plan.Id).MyFeedback.Should().BeNull();
        (FileOf(refreshed.Value, plan.Id).GenerationId == originalGeneration).Should().BeFalse();
        var row = await LoadFileSummaryAsync(fixture, plan.Id);
        (await fixture.Context.CheckpointSummaryFeedbacks.CountAsync(item => item.SubmissionFileSummaryId == row.Id)).Should().Be(1);

        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton(EnabledSummaries)));
        using var client = app.CreateClient();
        var tokens = fixture.Scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var reviewer = await fixture.Context.Users.AsNoTracking().SingleAsync(item => item.Id == fixture.Seed.LecturerId);
        using var staleRating = new HttpRequestMessage(HttpMethod.Put,
            $"/api/workspace/checkpoints/teams/{teamId}/checkpoints/1/ai-summary/files/{plan.Id}/feedback");
        staleRating.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(reviewer, [SystemRoles.Lecturer]).Token);
        staleRating.Content = JsonContent.Create(new SetCheckpointSummaryFeedbackRequest { Rating = "Helpful", GenerationId = originalGeneration });
        using var conflict = await client.SendAsync(staleRating);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await feedback.SetAsync(teamId, 1, plan.Id, new SetCheckpointSummaryFeedbackRequest { Rating = "NotHelpful", Reason = "MissingKeyPoint",
            GenerationId = FileOf(refreshed.Value, plan.Id).GenerationId },
            fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        var rated = await CreateSummaryHandler(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        FileOf(rated.Value, plan.Id).MyFeedback!.Rating.Should().Be("NotHelpful");
        using var staleClear = new HttpRequestMessage(HttpMethod.Delete, staleRating.RequestUri + $"?generationId={originalGeneration}");
        staleClear.Headers.Authorization = staleRating.Headers.Authorization;
        using var clearConflict = await client.SendAsync(staleClear);
        clearConflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var unchanged = await CreateSummaryHandler(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        FileOf(unchanged.Value, plan.Id).MyFeedback!.Rating.Should().Be("NotHelpful");
    }

    [Fact]
    public async Task SummaryJob_ChangedChecklistCanBeRefreshedWithoutReusingOldRubricEvidence()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var plan = await fixture.UploadStoredFileAsync("rubric-demo.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        await SetRequirementsAsync(fixture, "Khảo sát khách hàng");
        var model = new ScriptedSummarizer(_ => Result.Success(Answer()));
        await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer);
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, model), fixture.Clock.UtcNow);
        var current = await CreateSummaryHandler(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        FileOf(current.Value, plan.Id).NeedsRefresh.Should().BeFalse();

        await SetRequirementsAsync(fixture, "Phân tích đối thủ");
        var stale = await CreateSummaryHandler(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        FileOf(stale.Value, plan.Id).NeedsRefresh.Should().BeTrue();
        stale.Value.Requirements.First().Finding.Should().Be("NotChecked");
        model.Prompts.Should().HaveCount(1, "reading a changed rubric must not silently call the model");

        await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer);
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, model), fixture.Clock.UtcNow);
        var refreshed = await CreateSummaryHandler(fixture).GetAsync(teamId, 1, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        FileOf(refreshed.Value, plan.Id).NeedsRefresh.Should().BeFalse();
        refreshed.Value.Requirements.First().Finding.Should().Be("NotFound");
        await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer);
        (await DrainSummariesAsync(CreateSummaryGenerator(fixture, model), fixture.Clock.UtcNow)).Should().Be(0);
        model.Prompts.Should().HaveCount(2);
    }

    [Fact]
    public async Task SummaryJob_OlderPromptIsExposedAsRefreshableWithoutDroppingThePreviousResult()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var plan = await fixture.UploadStoredFileAsync("prompt-demo.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        var model = new ScriptedSummarizer(_ => Result.Success(Answer()));
        await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer);
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, model), fixture.Clock.UtcNow);
        await fixture.Context.SubmissionFileSummaries.Where(item => item.SubmissionFileId == plan.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PromptVersion, "v11"));

        var response = await CreateSummaryHandler(fixture).GetAsync(teamId, 1, fixture.Seed.MentorUserId, SystemRoles.Mentor);
        FileOf(response.Value, plan.Id).Status.Should().Be("Ready");
        FileOf(response.Value, plan.Id).NeedsRefresh.Should().BeTrue();
        FileOf(response.Value, plan.Id).Content.Should().NotBeNull();
        await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer);
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, model), fixture.Clock.UtcNow);
        model.Prompts.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SummaryJob_LongTimeoutStartsTheRetryAndCircuitCooldownWhenTheFailureFinishes(bool tripCircuit)
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var plan = await fixture.UploadStoredFileAsync("timeout-demo.docx", BuildDocx(SurveyText));
        await CreateSummaryHandler(fixture).RequestAsync(fixture.Seed.TeamId!.Value, 1,
            Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer);
        var started = fixture.Clock.UtcNow;
        var finished = started.AddMinutes(10);
        var breaker = new CheckpointSummaryCircuitBreaker();
        if (tripCircuit)
        {
            breaker.RecordFailure(started.AddMinutes(-2));
            breaker.RecordFailure(started.AddMinutes(-1));
        }
        var model = new ScriptedSummarizer(_ =>
        {
            fixture.Clock.UtcNow = finished;
            return Result.Failure<string>(ErrorCodes.WorkspaceSummaryModelTimeout, "Synthetic timeout");
        });

        await CreateSummaryGenerator(fixture, model, breaker: breaker).ProcessNextAsync(started, default);

        var row = await LoadFileSummaryAsync(fixture, plan.Id);
        row.Status.Should().Be(CheckpointSummaryStatus.Pending);
        row.NextAttemptAtUtc.Should().BeCloseTo(finished.AddMinutes(tripCircuit ? 2 : 1), TimeSpan.FromMilliseconds(1));
        if (tripCircuit) breaker.AllowRequest(finished).Should().BeFalse();
    }
}
