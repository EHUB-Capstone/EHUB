namespace EHub.Shared.Constants;

/// <summary>
/// Bounds for the AI checkpoint summary. The model runs locally on a shared CPU VPS, so work is queued and
/// processed one summary at a time. Values come from the local benchmark in docs/workspace/AI_CHECKPOINT_SUMMARY.md.
/// </summary>
public static class CheckpointSummaryLimits
{
    /// <summary>Reviewer-requested files pending or processing across the system; new requests are refused beyond this.</summary>
    public const int MaxQueuedSummaries = 30;

    /// <summary>Files queued automatically after upload; beyond this, uploads are not queued (reviewers can still ask).</summary>
    public const int MaxQueuedAutomaticSummaries = 200;

    /// <summary>Attempts before a summary is marked Failed.</summary>
    public const int MaxAttempts = 3;

    /// <summary>Wait before retry number 1, 2, ... (the last entry is reused).</summary>
    public static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    /// <summary>
    /// Temporary failures (model not reachable, timeout, storage down) are retried for about an hour and a half, so a
    /// restart of the model container does not leave files failed. Attempts before such a summary is marked Failed.
    /// </summary>
    public const int MaxInfrastructureAttempts = 5;

    /// <summary>Wait before infrastructure retry number 1, 2, ... (the last entry is reused).</summary>
    public static readonly TimeSpan[] InfrastructureRetryDelays =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    /// <summary>A claimed summary is not picked up again for this long; longer than the job timeout.</summary>
    public static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Files larger than this are not downloaded for summarising: the upload limit, so no accepted file is left out. Slide
    /// decks exported with images are routinely 50-100 MB for a few thousand characters of text; reading a 62 MB deck took
    /// half a second and about its own size in memory, and files are read one at a time.
    /// </summary>
    public const long MaxFileSizeBytes = SubmissionFileLimits.MaxFileSizeBytes;

    public const int MaxOverviewCharacters = 1_500;
    public const int MaxItemCharacters = 500;
    public const int MaxEvidenceCharacters = 300;
    public const int MaxKeyPoints = 4;
    public const int MaxQuestions = 2;

    public const int RequestsPerUserPerHour = 10;
}
