namespace EHub.Infrastructure.Mentoring.Gemini;

/// <summary>Bounded, in-memory state shared by requests in this backend instance.</summary>
public sealed class GeminiRequestCoordinator
{
    internal readonly SemaphoreSlim Gate = new(1, 1);
    internal readonly Dictionary<string, DateTimeOffset> ModelBlockedUntil = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, GeminiCachedResponse> Cache = new(StringComparer.Ordinal);
    internal DateTimeOffset NextRequestAt;
    internal DateTimeOffset ProjectBlockedUntil;
}

internal sealed record GeminiCachedResponse(string Json, string Model, DateTimeOffset ExpiresAt);
