using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;
using EHub.Infrastructure.Mentoring.Gemini;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class GeminiMentorResilienceTests
{
    private const string Primary = "gemini-3.5-flash-lite";
    private const string Fallback = "gemini-3.1-flash-lite";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AStalledModelTimesOutBeforeHeadersOrDuringBodyAndUsesFallback(bool bodyStalls)
    {
        var stalled = Signal();
        using var rig = new Rig((model, ct) => model == Primary
            ? Stall(bodyStalls, stalled, ct) : Task.FromResult(Success()));
        var ranking = rig.RankAsync();
        await stalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        rig.Clock.Advance(TimeSpan.FromSeconds(55));

        var result = await ranking.WaitAsync(TimeSpan.FromSeconds(5));
        result.Values.Single().Values.Single().HasEvidence.Should().BeTrue();
        rig.Attempts.Select(call => call.Model).Should().Equal(Primary, Fallback);
        rig.Coordinator.ModelBlockedUntil[Primary].Should().Be(rig.Clock.GetUtcNow().AddMinutes(1));
        rig.Logger.Messages.Should().Contain(message => message.Contains("Timeout", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationDuringHeadersOrBodyStopsWithoutFallback(bool bodyStalls)
    {
        var stalled = Signal();
        using var rig = new Rig((_, ct) => Stall(bodyStalls, stalled, ct));
        using var caller = new CancellationTokenSource();
        var ranking = rig.RankAsync(caller.Token);
        await stalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        caller.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ranking.WaitAsync(TimeSpan.FromSeconds(5)));
        rig.Attempts.Should().ContainSingle();
        rig.Coordinator.ModelBlockedUntil.Should().BeEmpty("caller cancellation must not mark a healthy model unavailable");
        rig.Coordinator.Gate.CurrentCount.Should().Be(1);
    }

    [Fact]
    public async Task AnOuterExecutionBudgetEndsBeforeTheModelTimeoutAndDoesNotUseFallback()
    {
        var stalled = Signal();
        using var rig = new Rig((_, ct) => Stall(false, stalled, ct));
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30), rig.Clock);
        var ranking = rig.RankAsync(budget.Token);
        await stalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        rig.Clock.Advance(TimeSpan.FromSeconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ranking.WaitAsync(TimeSpan.FromSeconds(5)));
        rig.Attempts.Should().ContainSingle();
        rig.Coordinator.ModelBlockedUntil.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task TwoTransientResponsesRetryTheSameModelWithBackoffThenSucceed(HttpStatusCode status)
    {
        var calls = 0;
        using var rig = new Rig((_, _) => Task.FromResult(Interlocked.Increment(ref calls) < 3 ? Error(status) : Success()));
        var ranking = rig.RankAsync();

        await AdvanceRetryAsync(rig);
        await AdvanceRetryAsync(rig);

        (await ranking.WaitAsync(TimeSpan.FromSeconds(5))).Should().ContainSingle();
        var attempts = rig.Attempts.ToArray();
        attempts.Select(call => call.Model).Should().Equal(Primary, Primary, Primary);
        (attempts[1].At - attempts[0].At).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(2))
            .And.BeLessThan(TimeSpan.FromSeconds(3));
        (attempts[2].At - attempts[1].At).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(4))
            .And.BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TransientTransportFailuresRetryWithoutLeakingTheirExceptionMessage()
    {
        var calls = 0;
        using var rig = new Rig((_, _) => Interlocked.Increment(ref calls) < 3
            ? throw new HttpRequestException("PRIVATE_TRANSPORT_DETAIL") : Task.FromResult(Success()));
        var ranking = rig.RankAsync();

        await AdvanceRetryAsync(rig);
        await AdvanceRetryAsync(rig);

        (await ranking.WaitAsync(TimeSpan.FromSeconds(5))).Should().ContainSingle();
        rig.Attempts.Select(call => call.Model).Should().Equal(Primary, Primary, Primary);
        string.Join('\n', rig.Logger.Messages).Should().Contain("transport").And.NotContain("PRIVATE_TRANSPORT_DETAIL");
    }

    [Fact]
    public async Task NormalRequestPacingRemainsTheMinimumDuringTransientRetries()
    {
        var calls = 0;
        using var rig = new Rig((_, _) => Task.FromResult(Interlocked.Increment(ref calls) < 3
            ? Error(HttpStatusCode.ServiceUnavailable) : Success()), minimumInterval: 12);
        var ranking = rig.RankAsync();

        await AdvanceRetryAsync(rig);
        await AdvanceRetryAsync(rig);

        await ranking.WaitAsync(TimeSpan.FromSeconds(5));
        var attempts = rig.Attempts.ToArray();
        attempts.Should().HaveCount(3);
        (attempts[1].At - attempts[0].At).Should().Be(TimeSpan.FromSeconds(12));
        (attempts[2].At - attempts[1].At).Should().Be(TimeSpan.FromSeconds(12));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryAfterDeltaOrDateDelaysTheSameModelRetryBeyondNormalPacing(bool dateHeader)
    {
        var calls = 0;
        Rig? activeRig = null;
        using var rig = new Rig((_, _) => Task.FromResult(Interlocked.Increment(ref calls) == 1
            ? ErrorWithRetryAfter(dateHeader, TimeSpan.FromSeconds(20), activeRig!.Clock) : Success()), minimumInterval: 12);
        activeRig = rig;
        var ranking = rig.RankAsync();

        await AdvanceRetryAsync(rig);

        (await ranking.WaitAsync(TimeSpan.FromSeconds(5))).Should().ContainSingle();
        var attempts = rig.Attempts.ToArray();
        attempts.Select(call => call.Model).Should().Equal(Primary, Primary);
        (attempts[1].At - attempts[0].At).Should().Be(TimeSpan.FromSeconds(20));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exhausted503RetriesKeepALongerRetryAfterInsteadOfTheOneMinuteCooldown(bool dateHeader)
    {
        var calls = 0;
        Rig? activeRig = null;
        using var rig = new Rig((_, _) => Task.FromResult(Interlocked.Increment(ref calls) == 3
            ? ErrorWithRetryAfter(dateHeader, TimeSpan.FromMinutes(2), activeRig!.Clock)
            : Error(HttpStatusCode.ServiceUnavailable)), fallbackModels: "");
        activeRig = rig;
        var ranking = rig.RankAsync();

        await AdvanceRetryAsync(rig);
        await AdvanceRetryAsync(rig);

        var error = await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => ranking.WaitAsync(TimeSpan.FromSeconds(5)));
        error.Failure.Should().Be(MentorFitFailure.ServiceUnavailable);
        rig.Attempts.Select(call => call.Model).Should().Equal(Primary, Primary, Primary);
        // HTTP-date has second precision, so this comparison accounts for truncation of the jittered clock.
        (rig.Coordinator.ModelBlockedUntil[Primary] - rig.Clock.GetUtcNow()).Should()
            .BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(dateHeader ? 119 : 120));
    }

    [Fact]
    public async Task InvalidOutputIsRetriedOnFallbackWithoutAcceptingInventedEvidenceOrBlockingTheModel()
    {
        using var rig = new Rig((model, _) => Task.FromResult(model == Primary
            ? Success("INVENTED_EVIDENCE") : Success()));

        var result = await rig.RankAsync().WaitAsync(TimeSpan.FromSeconds(5));

        result.Values.Single().Values.Single().Criteria!.Experience.Should().Be(75);
        rig.Attempts.Select(call => call.Model).Should().Equal(Primary, Fallback);
        rig.Coordinator.ModelBlockedUntil.Should().BeEmpty("invalid output concerns the input, not the entire model");
        string.Join('\n', rig.Logger.Messages).Should().Contain("criterion_evidence").And.NotContain("INVENTED_EVIDENCE");
    }

    [Fact]
    public async Task AStalled429BodyKeepsTheSharedQuotaFailureAndNeverUsesFallback()
    {
        var stalled = Signal();
        using var rig = new Rig((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            { Content = new StreamContent(new StalledStream(stalled)) }));
        var ranking = rig.RankAsync();
        await stalled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        rig.Clock.Advance(TimeSpan.FromSeconds(55));

        var error = await Assert.ThrowsAsync<MentorFitRankerUnavailableException>(() => ranking.WaitAsync(TimeSpan.FromSeconds(5)));
        error.Failure.Should().Be(MentorFitFailure.Quota);
        rig.Attempts.Should().ContainSingle();
        rig.Coordinator.ProjectBlockedUntil.Should().BeAfter(rig.Clock.GetUtcNow());
    }

    private static async Task AdvanceRetryAsync(Rig rig)
    {
        // Request timeout timers are 55s. Advance only the short backoff/pacing timer after a response completes.
        var limit = Stopwatch.StartNew();
        while (rig.Clock.NextDelay is not { } delay || delay >= TimeSpan.FromSeconds(30))
        {
            if (limit.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("The retry timer was not scheduled.");
            await Task.Delay(1);
        }
        rig.Clock.Advance(rig.Clock.NextDelay!.Value);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<HttpResponseMessage> Stall(bool bodyStalls, TaskCompletionSource started, CancellationToken ct)
    {
        if (bodyStalls)
            return new(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream(started)) };
        started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        throw new InvalidOperationException("Unreachable after cancellation.");
    }

    private static HttpResponseMessage Error(HttpStatusCode status) => new(status)
    { Content = JsonContent.Create(new { error = new { message = "SYNTHETIC_PROVIDER_FAILURE" } }) };

    private static HttpResponseMessage ErrorWithRetryAfter(bool dateHeader, TimeSpan delay, ManualClock clock)
    {
        var response = Error(HttpStatusCode.ServiceUnavailable);
        response.Headers.RetryAfter = dateHeader
            ? new RetryConditionHeaderValue(clock.GetUtcNow() + delay) : new RetryConditionHeaderValue(delay);
        return response;
    }

    private static HttpResponseMessage Success(string evidence = "E01")
    {
        var text = JsonSerializer.Serialize(new
        {
            results = new[] { new { project = "P001", mentors = new[] { new { id = "M001", criteria = new
            {
                experience = new { score = 75, evidence },
                expertise = new { score = 0, evidence = "" }, startupDomain = new { score = 0, evidence = "" },
                technology = new { score = 0, evidence = "" }, bio = new { score = 0, evidence = "" }
            } } } } }
        });
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
        { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text } } } } } }) };
    }

    private sealed record Attempt(string Model, DateTimeOffset At);

    private sealed class Rig : IDisposable
    {
        public ManualClock Clock { get; } = new();
        public GeminiRequestCoordinator Coordinator { get; } = new();
        public ConcurrentQueue<Attempt> Attempts { get; } = new();
        public RecordingLogger Logger { get; } = new();
        private readonly HttpClient client;
        private readonly GeminiMentorFitRanker ranker;

        public Rig(Func<string, CancellationToken, Task<HttpResponseMessage>> send, int minimumInterval = 0,
            string fallbackModels = Fallback)
        {
            client = new HttpClient(new Handler((request, ct) =>
            {
                var model = request.RequestUri!.AbsolutePath.Split('/').Last().Split(':')[0];
                Attempts.Enqueue(new(model, Clock.GetUtcNow()));
                return send(model, ct);
            })) { Timeout = Timeout.InfiniteTimeSpan };
            ranker = new(client, Options.Create(new GeminiMentorOptions
            {
                ApiKey = "SYNTHETIC_RESILIENCE_KEY_NOT_REAL", DataTransferEnabled = true,
                Model = Primary, FallbackModels = fallbackModels, MinimumRequestIntervalSeconds = minimumInterval
            }), Coordinator, Clock, Logger);
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>> RankAsync(CancellationToken ct = default) =>
            ranker.RankAsync([new(Guid.NewGuid(), "Synthetic marketing startup needing business mentoring")],
                [new MentorProfile { Experience = "Marketing coaching", User = new User
                    { FullName = "Synthetic Mentor", Email = "resilience@example.test" } }], MentorRankingScope.AllSpecialties, ct);

        public void Dispose() { client.Dispose(); Coordinator.Gate.Dispose(); }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }

    private sealed class StalledStream(TaskCompletionSource started) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return 0;
        }
    }

    private sealed class RecordingLogger : ILogger<GeminiMentorFitRanker>
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Enqueue(formatter(state, exception));
    }

    private sealed class ManualClock : TimeProvider
    {
        private readonly object sync = new();
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() { lock (sync) return now; }
        public TimeSpan? NextDelay
        {
            get
            {
                lock (sync)
                {
                    var next = timers.Where(timer => timer.Due is not null).MinBy(timer => timer.Due);
                    return next?.Due - now;
                }
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (sync)
            {
                var timer = new ManualTimer(this, callback, state);
                timers.Add(timer);
                timer.Change(dueTime, period);
                return timer;
            }
        }

        public void Advance(TimeSpan elapsed)
        {
            lock (sync) now += elapsed;
            while (true)
            {
                ManualTimer? due;
                lock (sync)
                {
                    due = timers.FirstOrDefault(timer => timer.Due <= now);
                    if (due is null) return;
                    due.Due = due.Period == Timeout.InfiniteTimeSpan ? null : due.Due + due.Period;
                }
                due.Fire();
            }
        }

        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            public DateTimeOffset? Due;
            public TimeSpan Period;
            private bool disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (clock.sync)
                {
                    if (disposed) return false;
                    Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime;
                    Period = period;
                    return true;
                }
            }
            public void Fire() => callback(state);
            public void Dispose() { lock (clock.sync) { disposed = true; Due = null; } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
