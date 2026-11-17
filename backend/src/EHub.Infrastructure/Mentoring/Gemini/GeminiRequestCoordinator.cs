using System.Collections.Concurrent;
using EHub.Application.Common.Interfaces.Services;

namespace EHub.Infrastructure.Mentoring.Gemini;

/// <summary>
/// Bounded, in-memory state shared by requests in this backend instance. <see cref="Gate"/> limits how many Gemini calls
/// are in flight at once (1 keeps the original strictly sequential behaviour); the rest of the state is safe to share
/// between those concurrent calls.
/// </summary>
public sealed class GeminiRequestCoordinator
{
    private readonly object pacing = new();
    private readonly SemaphoreSlim requestStarts = new(1, 1);
    private DateTimeOffset nextRequestAt;
    private DateTimeOffset projectBlockedUntil;

    public GeminiRequestCoordinator(int maxConcurrentRequests = 1)
    {
        var slots = Math.Clamp(maxConcurrentRequests, 1, GeminiMentorOptions.MaximumConcurrentRequests);
        Gate = new SemaphoreSlim(slots, slots);
    }

    internal readonly SemaphoreSlim Gate;
    internal readonly ConcurrentDictionary<string, DateTimeOffset> ModelBlockedUntil = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, MentorFitFailure> ModelFailures = new(StringComparer.Ordinal);
    internal readonly ConcurrentDictionary<string, GeminiCachedResponse> Cache = new(StringComparer.Ordinal);

    internal DateTimeOffset NextRequestAt
    {
        get { lock (pacing) return nextRequestAt; }
        set { lock (pacing) nextRequestAt = value; }
    }

    internal DateTimeOffset ProjectBlockedUntil
    {
        get { lock (pacing) return projectBlockedUntil; }
        set { lock (pacing) if (value > projectBlockedUntil) projectBlockedUntil = value; }
    }

    internal void BlockModelUntil(string model, DateTimeOffset until, MentorFitFailure failure)
    {
        lock (pacing)
        {
            if (ModelBlockedUntil.TryGetValue(model, out var current) && current >= until) return;
            ModelFailures[model] = failure;
            ModelBlockedUntil[model] = until;
        }
    }

    /// <summary>
    /// Serialize only request starts, not network responses. Waiting callers recheck provider cooldowns after every
    /// delay, so a later Retry-After/shared quota applies even to another allocation already waiting to send.
    /// </summary>
    internal async Task<T> StartRequestAsync<T>(TimeProvider clock, TimeSpan interval, Action ensureAllowed,
        Func<T> start, CancellationToken cancellationToken)
    {
        await requestStarts.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                TimeSpan delay;
                DateTimeOffset reserved;
                lock (pacing)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ensureAllowed();
                    var now = clock.GetUtcNow();
                    reserved = nextRequestAt > now ? nextRequestAt : now;
                    delay = reserved - now;
                }
                if (delay > TimeSpan.Zero) await Task.Delay(delay, clock, cancellationToken);
                lock (pacing)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ensureAllowed();
                    // Only provider backoff can move this while requestStarts is held. If it changed, wait again.
                    if (nextRequestAt > reserved) continue;
                    var now = clock.GetUtcNow();
                    nextRequestAt = (now > reserved ? now : reserved) + interval;
                    return start();
                }
            }
        }
        finally { requestStarts.Release(); }
    }

    /// <summary>
    /// Atomically claims the next start time, at least <paramref name="interval"/> after the previous claim, and returns how
    /// long the caller must wait for it. Concurrent callers therefore start one interval apart instead of all at once.
    /// </summary>
    internal TimeSpan ReserveRequestStart(DateTimeOffset now, TimeSpan interval)
    {
        lock (pacing)
        {
            var start = nextRequestAt > now ? nextRequestAt : now;
            nextRequestAt = start + interval;
            return start - now;
        }
    }

    /// <summary>Pushes the next start later (provider Retry-After, backoff); never earlier.</summary>
    internal void DelayNextRequestUntil(DateTimeOffset moment)
    {
        lock (pacing) if (moment > nextRequestAt) nextRequestAt = moment;
    }
}

internal sealed record GeminiCachedResponse(string Json, string Model, DateTimeOffset ExpiresAt);
