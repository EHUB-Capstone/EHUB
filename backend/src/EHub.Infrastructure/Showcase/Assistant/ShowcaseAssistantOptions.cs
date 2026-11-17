using EHub.Infrastructure.Mentoring.Gemini;

namespace EHub.Infrastructure.Showcase.Assistant;

public sealed class ShowcaseAssistantOptions
{
    public const string SectionName = "Showcase:Assistant";

    /// <summary>
    /// Sends published showcase text (name, description, problem, solution, target users, team name) to Google Gemini.
    /// Enable only after confirming this is acceptable. When ApiKey is empty it falls back to Mentoring:Gemini:ApiKey.
    /// </summary>
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.5-flash-lite";
    public string FallbackModels { get; set; } = "gemini-3.1-flash-lite";

    internal string[] Models => new[] { Model }.Concat(FallbackModels.Split(',',
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal).ToArray();

    internal bool IsValid() => Enabled && ApiKey.Length is > 0 and <= 2048 &&
        ApiKey.All(character => character is >= '!' and <= '~') &&
        Models.Length is >= 1 and <= 3 &&
        Models.All(model => GeminiMentorOptions.ApprovedModels.Contains(model, StringComparer.Ordinal));
}
