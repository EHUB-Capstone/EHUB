namespace EHub.Application.Features.Showcase.Assistant;

/// <summary>
/// The user's own AI provider key, as it arrives in a request header. Only its shape is checked here (the provider decides
/// whether it works); this keeps junk and anything that could break out of a header from ever reaching the provider call.
/// </summary>
public static class ShowcaseAssistantApiKey
{
    public const string HeaderName = "X-Gemini-Api-Key";

    // Gemini keys are about 40 characters; the bounds only reject an empty paste and an oversized one.
    public const int MinimumLength = 20;
    public const int MaximumLength = 256;

    /// <summary>Printable ASCII with no spaces, and not a pasted URL or "NAME=value" line.</summary>
    public static bool IsWellFormed(string? key) =>
        key is { Length: >= MinimumLength and <= MaximumLength } &&
        key.All(character => character is >= '!' and <= '~') &&
        !key.Contains("://", StringComparison.Ordinal) && !key.Contains('=');
}
