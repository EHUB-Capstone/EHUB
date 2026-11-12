using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Domain.Enums;
using EHub.Infrastructure.Summarization;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Classes;

// The circuit that pauses the summary worker while the model is down, and the administrators' diagnostics.
public sealed partial class TeamWorkflowIntegrationTests
{
    [Fact]
    public async Task SummaryJob_PausesWithoutSpendingAttemptsWhileTheModelKeepsFailingThenRecovers()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var first = await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        var second = await fixture.UploadStoredFileAsync("update.docx", BuildDocx(CompetitorText));
        var teamId = fixture.Seed.TeamId!.Value;
        (await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(first.Id, second.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer))
            .IsSuccess.Should().BeTrue();
        var breaker = new CheckpointSummaryCircuitBreaker();
        var down = new ScriptedSummarizer(_ => Result.Failure<string>(ErrorCodes.WorkspaceSummaryModelUnreachable, "down"));
        var now = fixture.Clock.UtcNow;

        // Round 1: each file fails once and goes back with its normal one-minute back-off. Two failures do not open the circuit.
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, down, breaker: breaker), now);
        down.Prompts.Should().HaveCount(2);
        breaker.Snapshot(now).State.Should().Be(CheckpointCircuitState.Closed);

        // Round 2: the third failure in a row opens the circuit, so the other file is not even tried.
        var retry = now + CheckpointSummaryLimits.InfrastructureRetryDelays[0];
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, down, breaker: breaker), retry);
        down.Prompts.Should().HaveCount(3, "once the circuit is open no file is sent to the model");
        var openUntil = breaker.Snapshot(retry).OpenUntilUtc!.Value;
        openUntil.Should().Be(retry.AddMinutes(2));
        var rows = new[] { await LoadFileSummaryAsync(fixture, first.Id), await LoadFileSummaryAsync(fixture, second.Id) };
        rows.Should().OnlyContain(row => row.Status == CheckpointSummaryStatus.Pending);
        rows.Count(row => row.AttemptCount == 1).Should().Be(2, "the paused file keeps the attempts it had; the circuit does not spend one");
        rows.Count(row => row.NextAttemptAtUtc is { } next && (next - openUntil).Duration() < TimeSpan.FromMilliseconds(1)).Should().Be(1, "the file that tripped the circuit waits for the cool-down");

        // After the cool-down the model answers again: both files are summarised and the circuit closes.
        var working = new ScriptedSummarizer(_ => Result.Success(Answer()));
        await DrainSummariesAsync(CreateSummaryGenerator(fixture, working, breaker: breaker), openUntil);
        (await LoadFileSummaryAsync(fixture, first.Id)).Status.Should().Be(CheckpointSummaryStatus.Ready);
        (await LoadFileSummaryAsync(fixture, second.Id)).Status.Should().Be(CheckpointSummaryStatus.Ready);
        breaker.Snapshot(openUntil).State.Should().Be(CheckpointCircuitState.Closed);
    }

    [Fact]
    public async Task SummaryJob_DoesNotCountABadAnswerAgainstTheCircuit()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var plan = await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        (await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        var breaker = new CheckpointSummaryCircuitBreaker();
        var confused = new ScriptedSummarizer(_ => Result.Failure<string>(ErrorCodes.WorkspaceSummaryModelBadAnswer, "bad"));
        var now = fixture.Clock.UtcNow;

        for (var attempt = 0; attempt < CheckpointSummaryLimits.MaxAttempts; attempt++)
        {
            await DrainSummariesAsync(CreateSummaryGenerator(fixture, confused, breaker: breaker), now);
            now = (await LoadFileSummaryAsync(fixture, plan.Id)).NextAttemptAtUtc ?? now;
        }

        (await LoadFileSummaryAsync(fixture, plan.Id)).Status.Should().Be(CheckpointSummaryStatus.Failed);
        breaker.Snapshot(now).ConsecutiveFailures.Should().Be(0, "the model answered; it was the answer that was unusable");
    }

    [Fact]
    public async Task SummaryDiagnostics_ShowTheQueueTheCircuitAndTheModelToAdministratorsOnly()
    {
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var plan = await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText));
        var teamId = fixture.Seed.TeamId!.Value;
        (await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(plan.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        var breaker = new CheckpointSummaryCircuitBreaker();
        var summarizer = new ScriptedSummarizer(_ => Result.Success(Answer()));

        fixture.Context.ChangeTracker.Clear();
        CheckpointSummaryDiagnosticsHandler Diagnostics(CheckpointSummarySettings? settings = null) =>
            new(fixture.Context, fixture.Clock, settings ?? EnabledSummaries, summarizer, breaker);

        var waiting = await Diagnostics().GetAsync(SystemRoles.Admin);
        waiting.IsSuccess.Should().BeTrue();
        waiting.Value.PendingRequested.Should().Be(1);
        waiting.Value.PendingAutomatic.Should().Be(0);
        waiting.Value.Processing.Should().Be(0);
        waiting.Value.OldestPendingSeconds.Should().NotBeNull();
        waiting.Value.Model.Should().Be("scripted-model");
        waiting.Value.PromptVersion.Should().Be("v12");
        waiting.Value.ModelReachable.Should().BeTrue();
        waiting.Value.Circuit.Should().Be("Closed");

        await DrainSummariesAsync(CreateSummaryGenerator(fixture, summarizer, breaker: breaker), fixture.Clock.UtcNow);
        fixture.Context.ChangeTracker.Clear();
        var done = await Diagnostics().GetAsync(SystemRoles.Admin);
        done.Value.PendingRequested.Should().Be(0);
        done.Value.Ready.Should().BeGreaterThanOrEqualTo(1);
        done.Value.LastGeneratedAtUtc.Should().NotBeNull();
        // Counts and codes only: nothing from a submission or a file name.
        JsonSerializer.Serialize(done.Value).Should().NotContain("plan.docx").And.NotContain("nilon");

        (await Diagnostics().GetAsync(SystemRoles.Lecturer)).Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await Diagnostics().GetAsync(SystemRoles.Mentor)).Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await Diagnostics(new CheckpointSummarySettings { Enabled = false }).GetAsync(SystemRoles.Admin))
            .Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryDisabled);
    }

    [Fact]
    public async Task SummaryDiagnosticsApi_RequiresAnAdministratorAndIsOffWhenSummariesAreOff()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var tokens = fixture.Scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = await fixture.Context.Users.AsNoTracking().SingleAsync(item => item.Id == fixture.Seed.LecturerId);
        using var client = _factory.CreateClient();
        const string url = "/api/workspace/checkpoints/ai-summary/diagnostics";

        async Task<HttpStatusCode> Get(string? role)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (role is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(user, [role]).Token);
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        (await Get(null)).Should().Be(HttpStatusCode.Unauthorized);
        (await Get(SystemRoles.Lecturer)).Should().Be(HttpStatusCode.Forbidden);
        (await Get(SystemRoles.Student)).Should().Be(HttpStatusCode.Forbidden);
        // The test host keeps the default Summarization:Provider=Disabled.
        (await Get(SystemRoles.Admin)).Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>
    /// Stops and starts the real Ollama container while files are queued (opt-in: EHUB_TEST_OLLAMA_OUTAGE=1, with
    /// EHUB_TEST_SUMMARY_OLLAMA_URL and EHUB_TEST_OLLAMA_CONTAINER, default ehub-summary-bench). Back-off times are the test's clock,
    /// so nothing waits for the real cool-down; the model itself is real.
    /// </summary>
    [OllamaOutageFact]
    public async Task SummaryJob_SurvivesTheRealModelServerBeingStoppedAndStartedAgain()
    {
        var container = Environment.GetEnvironmentVariable("EHUB_TEST_OLLAMA_CONTAINER") ?? "ehub-summary-bench";
        var url = Environment.GetEnvironmentVariable("EHUB_TEST_SUMMARY_OLLAMA_URL") ?? "http://127.0.0.1:11434/";
        using var fixture = await CreateUploadFixtureAsync();
        await ClearSummaryQueueAsync(fixture);
        var first = await fixture.UploadStoredFileAsync("plan.docx", BuildDocx(SurveyText, "Giải pháp là túi vải bạt tái chế giá 45.000 đồng."));
        var second = await fixture.UploadStoredFileAsync("update.docx", BuildDocx(CompetitorText, "Nhóm giao hàng trong 2 ngày."));
        var teamId = fixture.Seed.TeamId!.Value;
        (await CreateSummaryHandler(fixture).RequestAsync(teamId, 1, Files(first.Id, second.Id), fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Summarization:Model"] = Environment.GetEnvironmentVariable("EHUB_TEST_SUMMARY_MODEL") ?? "qwen3.5:2b-q4_K_M"
        }).Build();
        using var http = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromMinutes(10) };
        var summarizer = new OllamaCheckpointSummarizer(http, configuration);
        var breaker = new CheckpointSummaryCircuitBreaker();
        var now = fixture.Clock.UtcNow;

        try
        {
            (await summarizer.CheckHealthAsync(CancellationToken.None)).Should().Be(new SummarizerHealth(true, true), "the server is up and has the model");

            await DockerAsync("stop", container);
            (await summarizer.CheckHealthAsync(CancellationToken.None)).Reachable.Should().BeFalse("the server is stopped");

            // Real connection failures: two rounds are enough to open the circuit, and nothing is spent once it is open.
            await DrainSummariesAsync(CreateSummaryGenerator(fixture, summarizer, breaker: breaker), now);
            var retry = now + CheckpointSummaryLimits.InfrastructureRetryDelays[0];
            await DrainSummariesAsync(CreateSummaryGenerator(fixture, summarizer, breaker: breaker), retry);
            var snapshot = breaker.Snapshot(retry);
            snapshot.State.Should().Be(CheckpointCircuitState.Open);
            var rows = new[] { await LoadFileSummaryAsync(fixture, first.Id), await LoadFileSummaryAsync(fixture, second.Id) };
            rows.Should().OnlyContain(row => row.Status == CheckpointSummaryStatus.Pending && row.AttemptCount == 1 &&
                row.LastErrorCode == ErrorCodes.WorkspaceSummaryModelUnreachable);

            await DockerAsync("start", container);
            await WaitForOllamaAsync(http);

            // The cool-down is over (the test's clock moves on): the model is back and both files are summarised.
            var after = snapshot.OpenUntilUtc!.Value;
            await DrainSummariesAsync(CreateSummaryGenerator(fixture, summarizer, breaker: breaker), after);
            (await LoadFileSummaryAsync(fixture, first.Id)).Status.Should().Be(CheckpointSummaryStatus.Ready);
            (await LoadFileSummaryAsync(fixture, second.Id)).Status.Should().Be(CheckpointSummaryStatus.Ready);
            breaker.Snapshot(after).State.Should().Be(CheckpointCircuitState.Closed);
        }
        finally
        {
            // Leave the container running whatever happened.
            await DockerAsync("start", container);
        }
    }

    private static async Task DockerAsync(string command, string container)
    {
        using var process = Process.Start(new ProcessStartInfo("docker", $"{command} {container}")
            { RedirectStandardOutput = true, RedirectStandardError = true })!;
        await process.WaitForExitAsync();
        process.ExitCode.Should().Be(0, $"docker {command} {container} must work for this test");
    }

    private static async Task WaitForOllamaAsync(HttpClient http)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try { if ((await http.GetAsync("api/tags")).IsSuccessStatusCode) return; }
            catch (HttpRequestException) { /* still starting */ }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        throw new TimeoutException("Ollama did not come back within two minutes.");
    }
}

public sealed class OllamaOutageFactAttribute : FactAttribute
{
    public OllamaOutageFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EHUB_TEST_OLLAMA_OUTAGE") != "1")
            Skip = "Set EHUB_TEST_OLLAMA_OUTAGE=1 (and EHUB_TEST_SUMMARY_OLLAMA_URL) to stop and start a real Ollama container.";
    }
}
