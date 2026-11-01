namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>Runtime switch and budget for AI checkpoint summaries, bound from the <c>Summarization</c> configuration section.</summary>
public sealed class CheckpointSummarySettings
{
    public bool Enabled { get; init; }

    /// <summary>Characters read per team/checkpoint; the rest is not summarised. 20,000 fits a 2B model in 3 GB.</summary>
    public int MaxInputCharacters { get; init; } = 20_000;

    /// <summary>Upper bound for one summary, including every model call.</summary>
    public TimeSpan JobTimeout { get; init; } = TimeSpan.FromMinutes(10);
}
