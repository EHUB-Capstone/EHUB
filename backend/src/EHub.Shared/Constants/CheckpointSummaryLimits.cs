namespace EHub.Shared.Constants;

/// <summary>
/// Bounds for the AI checkpoint summary. The model runs locally on a shared CPU VPS, so work is queued and
/// processed one summary at a time. Values come from the local benchmark in docs/workspace/AI_CHECKPOINT_SUMMARY.md.
/// </summary>
public static class CheckpointSummaryLimits
{
    /// <summary>Pending and processing summaries across the whole system; new requests are refused beyond this.</summary>
    public const int MaxQueuedSummaries = 30;

    /// <summary>Attempts before a summary is marked Failed.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Wait before retry number 1, 2, ... (the last entry is reused).</summary>
    public static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    /// <summary>A claimed summary is not picked up again for this long; longer than the job timeout.</summary>
    public static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(15);

    /// <summary>Files larger than this are not downloaded for summarising.</summary>
    public const long MaxFileSizeBytes = 30L * 1024 * 1024;

    /// <summary>A file up to this many characters is sent to the final prompt verbatim; longer files are summarised in chunks first.</summary>
    public const int RawFileCharacters = 4_000;

    public const int ChunkCharacters = 6_000;

    public const int MaxOverviewCharacters = 1_500;
    public const int MaxItemCharacters = 500;
    public const int MaxEvidenceCharacters = 300;
    public const int MaxKeyPoints = 7;
    public const int MaxQuestions = 5;

    public const int RequestsPerUserPerHour = 10;
}
