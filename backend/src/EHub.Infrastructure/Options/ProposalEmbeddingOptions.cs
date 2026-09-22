namespace EHub.Infrastructure.Options;

public sealed class ProposalEmbeddingOptions
{
    public const string SectionName = "AI:Embedding";

    public string Provider { get; init; } = "DeterministicLocal";
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = "gemini-embedding-2";
    public int Dimension { get; init; } = 768;
    public int TimeoutSeconds { get; init; } = 30;
}
