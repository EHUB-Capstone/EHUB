namespace EHub.Infrastructure.Options;

public sealed class ProposalAnalysisOptions
{
    public const string SectionName = "AI:Analysis";

    public string Provider { get; init; } = "Mock";
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gemini-2.5-flash";
    public int TimeoutSeconds { get; init; } = 60;
    public int MaximumOutputTokens { get; init; } = 4_096;
    public double Temperature { get; init; } = 0.1;
}
