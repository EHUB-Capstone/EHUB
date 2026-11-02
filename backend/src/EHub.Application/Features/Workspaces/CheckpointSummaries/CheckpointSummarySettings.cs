namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>Runtime switch and budget for AI file summaries, bound from the <c>Summarization</c> configuration section.</summary>
public sealed class CheckpointSummarySettings
{
    public bool Enabled { get; init; }

    /// <summary>Characters read from one file; the rest is not considered.</summary>
    public int MaxInputCharacters { get; init; } = 30_000;

    /// <summary>
    /// Characters of one file sent to the model. Longer files are condensed to their most informative sentences.
    /// 12,000 characters is about 4,500 tokens: it fits the 8k context, peaked at 2.5 GiB and took 1-2.5 minutes on PC.
    /// </summary>
    public int FilePromptCharacters { get; init; } = 12_000;

    /// <summary>Upper bound for one file summary.</summary>
    public TimeSpan JobTimeout { get; init; } = TimeSpan.FromMinutes(10);
}
