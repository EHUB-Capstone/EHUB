namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>Runtime switch and budget for AI file summaries, bound from the <c>Summarization</c> configuration section.</summary>
public sealed class CheckpointSummarySettings
{
    public bool Enabled { get; init; }

    /// <summary>
    /// Queue every uploaded file at low priority, so summaries are usually ready before a reviewer opens the
    /// checkpoint. The worker still runs one file at a time, so this lengthens the queue, not memory use.
    /// </summary>
    public bool AutoSummarize { get; init; } = true;

    /// <summary>Characters read from one file; the rest is not considered.</summary>
    public int MaxInputCharacters { get; init; } = 30_000;

    /// <summary>
    /// Characters of one file sent to the model. Longer files are condensed to their most informative sentences.
    /// With the short v4 answer, 5,000 characters took 21-32 s for typical files and about 70 s for long ones on PC.
    /// </summary>
    public int FilePromptCharacters { get; init; } = 5_000;

    /// <summary>Upper bound for one file summary.</summary>
    public TimeSpan JobTimeout { get; init; } = TimeSpan.FromMinutes(10);
}
