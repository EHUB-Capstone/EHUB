namespace EHub.Infrastructure.Mentoring.Gemini;

public sealed class GeminiMentorOptions
{
    public const string SectionName = "Mentoring:Gemini";
    // Free text input/output tiers checked on 2026-11-06. Project access/quota is not guaranteed.
    internal static readonly string[] ApprovedModels =
        ["gemini-3.8-flash", "gemini-3.5-flash-lite", "gemini-3.1-flash-lite", "gemini-2.5-flash-lite"];

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.5-flash-lite";
    public string FallbackModels { get; set; } = "gemini-3.1-flash-lite";
    // Enable only after reviewing the professional summaries; automatic masking is not anonymization.
    public bool DataTransferEnabled { get; set; }
    public int MinimumRequestIntervalSeconds { get; set; } = 12;

    internal string[] Models => new[] { Model }.Concat(FallbackModels.Split(',',
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).Distinct(StringComparer.Ordinal).ToArray();

    internal bool IsValid() => DataTransferEnabled && ApiKey.Length is > 0 and <= 2048 &&
        ApiKey.All(character => character is >= '!' and <= '~') &&
        Models.Length is >= 1 and <= 3 && Models.All(model => ApprovedModels.Contains(model, StringComparer.Ordinal)) &&
        MinimumRequestIntervalSeconds is >= 0 and <= 120;
}
