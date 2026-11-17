using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;
using EHub.Infrastructure;
using EHub.Infrastructure.Mentoring.Gemini;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class GeminiMentorConcurrencyTests
{
    private const string Evidence = "Digital marketing and startup growth";
    private const string FakeKey = "OFFLINE_TEST_NOT_A_REAL_API_KEY=";
    private const int Batches = 5;

    [Fact]
    public async Task ConcurrentBatches_RunInParallelUpToTheLimit_ReturnEveryProject_AndReportProgressOneAtATime()
    {
        var progress = new List<(int Done, int Total)>();
        var overlapped = false;
        var reporting = 0;
        var execution = new MentorRankingExecution();
        execution.EnableBackground(async (done, total, _) =>
        {
            if (Interlocked.Increment(ref reporting) > 1) overlapped = true;
            await Task.Delay(5);
            progress.Add((done, total));
            Interlocked.Decrement(ref reporting);
        });
        await execution.BeginAsync(Batches, default);
        using var rig = new Rig(maxConcurrent: 3, execution: execution);

        var result = await rig.RankAsync(Projects());

        result.Should().HaveCount(Batches * MentorRankingExecution.ProjectsPerBatch);
        rig.MaxInFlight.Should().BeInRange(2, 3);
        rig.Calls.Should().Be(Batches);
        overlapped.Should().BeFalse("the progress callback writes through one DbContext and must never run twice at once");
        progress.Should().Equal(Enumerable.Range(0, Batches + 1).Select(done => (done, Batches)));
        rig.Coordinator.Gate.CurrentCount.Should().Be(3);
    }

    [Fact]
    public async Task DefaultSettings_KeepBatchesSequential()
    {
        using var rig = new Rig(maxConcurrent: 1);

        var result = await rig.RankAsync(Projects());

        result.Should().HaveCount(Batches * MentorRankingExecution.ProjectsPerBatch);
        rig.MaxInFlight.Should().Be(1);
    }

    [Fact]
    public async Task OneFailedBatch_StopsTheAllocationWithItsFailure_AndEveryOtherRequestEnds()
    {
        using var rig = new Rig(maxConcurrent: 3, failOnCall: 2);

        var act = () => rig.RankAsync(Projects());

        (await act.Should().ThrowAsync<MentorFitRankerUnavailableException>()).Which.Failure.Should().Be(MentorFitFailure.Permission);
        rig.InFlight.Should().Be(0, "RankAsync waits until the cancelled requests have really stopped");
        rig.Coordinator.Gate.CurrentCount.Should().Be(3, "every request slot is released after a failure");
    }

    [Fact]
    public void RequestStarts_AreReservedOneIntervalApart_AndRetryDelaysNeverMoveThemEarlier()
    {
        var coordinator = new GeminiRequestCoordinator(3);
        var now = new DateTimeOffset(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);
        var interval = TimeSpan.FromSeconds(12);

        coordinator.ReserveRequestStart(now, interval).Should().Be(TimeSpan.Zero);
        coordinator.ReserveRequestStart(now, interval).Should().Be(TimeSpan.FromSeconds(12));
        coordinator.ReserveRequestStart(now, interval).Should().Be(TimeSpan.FromSeconds(24));
        coordinator.DelayNextRequestUntil(now.AddSeconds(5));
        coordinator.NextRequestAt.Should().Be(now.AddSeconds(36));
        coordinator.DelayNextRequestUntil(now.AddSeconds(60));
        coordinator.NextRequestAt.Should().Be(now.AddSeconds(60));
    }

    [Fact]
    public async Task ASharedQuotaStopsAnotherAllocationAlreadyWaitingForItsRequestStart()
    {
        var firstResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var rig = new SharedRig((number, _, _, _) => number == 1
            ? firstResponse.Task : throw new InvalidOperationException("A queued request must not reach the provider."));
        var first = rig.RankAsync("first");
        await WaitForAsync(() => rig.Attempts.Count == 1);
        var second = rig.RankAsync("second");
        await WaitForAsync(() => rig.Clock.NextDelay == TimeSpan.FromSeconds(12));

        firstResponse.SetResult(Error(HttpStatusCode.TooManyRequests, 60));
        (await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => first)).Failure.Should().Be(MentorFitFailure.Quota);
        rig.Clock.Advance(TimeSpan.FromSeconds(12));

        (await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => second.WaitAsync(TimeSpan.FromSeconds(5))))
            .Failure.Should().Be(MentorFitFailure.Quota);
        rig.Attempts.Should().ContainSingle("these are separate RankAsync jobs, so sibling cancellation cannot hide an extra call");
        rig.Coordinator.Gate.CurrentCount.Should().Be(2);
    }

    [Fact]
    public async Task ARetryAfterDelaysAnotherAllocationThatWasAlreadyWaitingToSend()
    {
        var firstResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var rig = new SharedRig((number, _, body, _) => number == 1 ? firstResponse.Task : Task.FromResult(Envelope(Result(body).ToJsonString())));
        using var caller = new CancellationTokenSource();
        var first = rig.RankAsync("first", caller.Token);
        await WaitForAsync(() => rig.Attempts.Count == 1);
        var second = rig.RankAsync("second");
        await WaitForAsync(() => rig.Clock.NextDelay == TimeSpan.FromSeconds(12));

        firstResponse.SetResult(Error(HttpStatusCode.ServiceUnavailable, 60));
        await WaitForAsync(() => rig.Coordinator.NextRequestAt == rig.Start.AddSeconds(60));
        rig.Clock.Advance(TimeSpan.FromSeconds(12));
        await WaitForAsync(() => rig.Clock.NextDelay == TimeSpan.FromSeconds(48));
        rig.Clock.Advance(TimeSpan.FromSeconds(47));
        rig.Attempts.Should().ContainSingle("the old 12-second slot must not bypass the newer Retry-After");
        rig.Clock.Advance(TimeSpan.FromSeconds(1));

        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Should().ContainSingle();
        rig.Attempts.Select(attempt => attempt.At).Should().Equal(rig.Start, rig.Start.AddSeconds(60));
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(TimeSpan.FromSeconds(5)));
        rig.Coordinator.Gate.CurrentCount.Should().Be(2);
    }

    [Fact]
    public async Task AModelSpecificQuotaMovesAnAlreadyWaitingAllocationToItsFallback()
    {
        var firstResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var rig = new SharedRig((number, _, body, _) => number == 1 ? firstResponse.Task : Task.FromResult(Envelope(Result(body).ToJsonString())),
            fallback: "gemini-3.1-flash-lite");
        var first = rig.RankAsync("first");
        await WaitForAsync(() => rig.Attempts.Count == 1);
        var second = rig.RankAsync("second");
        await WaitForAsync(() => rig.Clock.NextDelay == TimeSpan.FromSeconds(12));
        var response = Error(HttpStatusCode.TooManyRequests, 60);
        response.Content = JsonContent.Create(new { error = new { details = new[] { new Dictionary<string, object>
        {
            ["@type"] = "type.googleapis.com/google.rpc.QuotaFailure", ["violations"] = new[] { new
            { quotaId = "RequestsPerModelPerMinute", quotaDimensions = new Dictionary<string, string> { ["model"] = "gemini-3.5-flash-lite" } } }
        } } } });

        firstResponse.SetResult(response);
        await WaitForAsync(() => rig.Coordinator.ModelBlockedUntil.ContainsKey("gemini-3.5-flash-lite"));
        rig.Clock.Advance(TimeSpan.FromSeconds(12));
        (await first.WaitAsync(TimeSpan.FromSeconds(5))).Should().ContainSingle();
        await WaitForAsync(() => rig.Clock.NextDelay == TimeSpan.FromSeconds(12));
        rig.Clock.Advance(TimeSpan.FromSeconds(12));
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Should().ContainSingle();

        rig.Attempts.Select(attempt => attempt.Model).Should().Equal("gemini-3.5-flash-lite", "gemini-3.1-flash-lite", "gemini-3.1-flash-lite");
        rig.Coordinator.Gate.CurrentCount.Should().Be(2);
    }

    [Fact]
    public async Task CancellingAQueuedStartReleasesItForAnotherAllocation()
    {
        using var rig = new SharedRig((_, _, body, _) => Task.FromResult(Envelope(Result(body).ToJsonString())));
        await rig.RankAsync("first");
        using var caller = new CancellationTokenSource();
        var cancelled = rig.RankAsync("cancelled", caller.Token);
        await WaitForAsync(() => rig.Clock.NextDelay == TimeSpan.FromSeconds(12));
        var next = rig.RankAsync("next");
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(TimeSpan.FromSeconds(5)));
        await WaitForAsync(() => rig.Clock.NextDelay == TimeSpan.FromSeconds(12));

        rig.Clock.Advance(TimeSpan.FromSeconds(12));

        (await next.WaitAsync(TimeSpan.FromSeconds(5))).Should().ContainSingle();
        rig.Attempts.Should().HaveCount(2);
        rig.Coordinator.Gate.CurrentCount.Should().Be(2);
    }

    [Fact]
    public void ConcurrentCooldownUpdatesCannotShortenTheExistingDeadlineOrChangeItsReason()
    {
        var coordinator = new GeminiRequestCoordinator(2);
        var now = DateTimeOffset.UtcNow;
        coordinator.ProjectBlockedUntil = now.AddHours(1);
        coordinator.ProjectBlockedUntil = now.AddMinutes(1);
        coordinator.ProjectBlockedUntil.Should().Be(now.AddHours(1));
        coordinator.BlockModelUntil("gemini-3.5-flash-lite", now.AddHours(1), MentorFitFailure.Quota);
        coordinator.BlockModelUntil("gemini-3.5-flash-lite", now.AddMinutes(1), MentorFitFailure.Network);
        coordinator.ModelBlockedUntil["gemini-3.5-flash-lite"].Should().Be(now.AddHours(1));
        coordinator.ModelFailures["gemini-3.5-flash-lite"].Should().Be(MentorFitFailure.Quota);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void MaxConcurrentRequests_MustBeBetweenOneAndFour(int value, bool valid)
    {
        var options = new GeminiMentorOptions { ApiKey = FakeKey, DataTransferEnabled = true, MaxConcurrentRequests = value };

        options.IsValid().Should().Be(valid);
    }

    [Fact]
    public void Configuration_SetsHowManyRequestsMayBeInFlight()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mentoring:Gemini:ApiKey"] = FakeKey, ["Mentoring:Gemini:DataTransferEnabled"] = "true",
            ["Mentoring:Gemini:MaxConcurrentRequests"] = "3"
        }).Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<GeminiRequestCoordinator>().Gate.CurrentCount.Should().Be(3);
    }

    private static MentorRankingProject[] Projects() => Enumerable.Range(0, Batches * MentorRankingExecution.ProjectsPerBatch)
        .Select(index => new MentorRankingProject(Guid.NewGuid(), $"Fictional startup {index}")).ToArray();

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("The offline concurrency scenario did not reach its expected state.");
            await Task.Delay(1);
        }
    }

    private static HttpResponseMessage Error(HttpStatusCode status, int retrySeconds)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("{}") };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retrySeconds));
        return response;
    }

    private sealed class SharedRig : IDisposable
    {
        public GeminiMentorResilienceTests.ManualClock Clock { get; } = new();
        public GeminiRequestCoordinator Coordinator { get; } = new(2);
        public ConcurrentQueue<(string Model, DateTimeOffset At)> Attempts { get; } = [];
        public DateTimeOffset Start { get; }
        private readonly HttpClient client;
        private readonly GeminiMentorFitRanker ranker;
        private int calls;
        public SharedRig(Func<int, string, string, CancellationToken, Task<HttpResponseMessage>> send, string fallback = "")
        {
            Start = Clock.GetUtcNow();
            client = new HttpClient(new Handler(async (request, ct) =>
            {
                var model = request.RequestUri!.AbsolutePath.Split('/').Last().Split(':')[0];
                Attempts.Enqueue((model, Clock.GetUtcNow()));
                return await send(Interlocked.Increment(ref calls), model, await request.Content!.ReadAsStringAsync(ct), ct);
            })) { Timeout = Timeout.InfiniteTimeSpan };
            ranker = new(client, Options.Create(new GeminiMentorOptions { ApiKey = FakeKey, DataTransferEnabled = true,
                MinimumRequestIntervalSeconds = 12, MaxConcurrentRequests = 2, FallbackModels = fallback }),
                Coordinator, Clock, NullLogger<GeminiMentorFitRanker>.Instance);
        }
        public Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankAsync(string marker, CancellationToken ct = default) =>
            ranker.RankAsync([new(Guid.NewGuid(), $"Synthetic marketing startup {marker}")], [Profile()], MentorRankingScope.AllSpecialties, ct);
        public void Dispose() { client.Dispose(); Coordinator.Gate.Dispose(); }
    }

    private sealed class Rig : IDisposable
    {
        private readonly HttpClient client;
        private readonly int failOnCall;
        private int inFlight;
        private int maxInFlight;
        private int calls;
        public GeminiRequestCoordinator Coordinator { get; }
        public GeminiMentorFitRanker Ranker { get; }
        public int InFlight => Volatile.Read(ref inFlight);
        public int MaxInFlight => Volatile.Read(ref maxInFlight);
        public int Calls => Volatile.Read(ref calls);

        public Rig(int maxConcurrent, MentorRankingExecution? execution = null, int failOnCall = 0)
        {
            this.failOnCall = failOnCall;
            Coordinator = new GeminiRequestCoordinator(maxConcurrent);
            client = new HttpClient(new Handler(SendAsync));
            var settings = new GeminiMentorOptions
            {
                ApiKey = FakeKey, DataTransferEnabled = true, MinimumRequestIntervalSeconds = 0,
                Model = "gemini-3.5-flash-lite", FallbackModels = "", MaxConcurrentRequests = maxConcurrent
            };
            Ranker = new(client, Options.Create(settings), Coordinator, TimeProvider.System,
                NullLogger<GeminiMentorFitRanker>.Instance, execution);
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankAsync(MentorRankingProject[] projects) =>
            Ranker.RankAsync(projects, [Profile()], MentorRankingScope.AllSpecialties, default);

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var current = Interlocked.Increment(ref inFlight);
            int seen;
            while (current > (seen = Volatile.Read(ref maxInFlight)) && Interlocked.CompareExchange(ref maxInFlight, current, seen) != seen) { }
            try
            {
                var number = Interlocked.Increment(ref calls);
                var body = await request.Content!.ReadAsStringAsync(token);
                // Hold the request open so overlapping batches are observable; honour cancellation like a real HTTP call.
                await Task.Delay(failOnCall == 0 || number == failOnCall ? 60 : 2_000, token);
                if (number == failOnCall) return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
                return Envelope(Result(body).ToJsonString());
            }
            finally { Interlocked.Decrement(ref inFlight); }
        }

        public void Dispose() { client.Dispose(); Coordinator.Gate.Dispose(); }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private static MentorProfile Profile() => new()
    {
        Experience = Evidence,
        User = new User { FullName = "Synthetic mentor", Email = "synthetic@example.test" }
    };

    private static JsonObject Result(string requestBody)
    {
        using var request = JsonDocument.Parse(requestBody);
        using var input = JsonDocument.Parse(request.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
        var firstMentor = input.RootElement.GetProperty("mentors")[0].GetProperty("id").GetString();
        var rows = new JsonArray();
        foreach (var project in input.RootElement.GetProperty("projects").EnumerateArray())
            rows.Add(new JsonObject { ["project"] = project.GetProperty("id").GetString(), ["mentors"] = new JsonArray
                { new JsonObject { ["id"] = firstMentor, ["criteria"] = Criteria() } } });
        return new JsonObject { ["results"] = rows };
    }

    private static JsonObject Criteria() => new()
    {
        ["experience"] = new JsonObject { ["score"] = 75, ["evidence"] = Evidence },
        ["expertise"] = new JsonObject { ["score"] = 0, ["evidence"] = "" },
        ["startupDomain"] = new JsonObject { ["score"] = 0, ["evidence"] = "" },
        ["technology"] = new JsonObject { ["score"] = 0, ["evidence"] = "" },
        ["bio"] = new JsonObject { ["score"] = 0, ["evidence"] = "" }
    };

    private static HttpResponseMessage Envelope(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = json } } } } },
            usageMetadata = new { promptTokenCount = 100, candidatesTokenCount = 50, totalTokenCount = 150 }
        }), Encoding.UTF8, "application/json")
    };
}
