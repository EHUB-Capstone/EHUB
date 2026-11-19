using EHub.Infrastructure.Mentoring.Gemini;

namespace EHub.Infrastructure.Showcase.Assistant;

public sealed class ShowcaseAssistantOptions
{
    public const string SectionName = "Showcase:Assistant";

    /// <summary>
    /// Switches the Showcase chat on. The server holds no AI key for it: each user pastes their own Gemini API key, which is
    /// used for that user's questions only and is never stored. Questions, recent chat turns and published showcase text
    /// (name, description, problem, solution, target users, team name) are then sent to Google Gemini under that key.
    /// </summary>
    public bool Enabled { get; set; }
    public string Model { get; set; } = "gemini-3.5-flash-lite";
    public string FallbackModels { get; set; } = "gemini-3.1-flash-lite";

    internal string[] Models => new[] { Model }.Concat(FallbackModels.Split(',',
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal).ToArray();

    internal bool IsValid() => Enabled &&
        Models.Length is >= 1 and <= 3 &&
        Models.All(model => GeminiMentorOptions.ApprovedModels.Contains(model, StringComparer.Ordinal));
}
