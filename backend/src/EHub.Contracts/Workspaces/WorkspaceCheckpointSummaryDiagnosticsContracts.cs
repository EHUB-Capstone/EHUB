namespace EHub.Contracts.Workspaces;

/// <summary>
/// For administrators only: why AI summaries are slow, stuck or failing. Counts and error codes, never file names, team names
/// or any text taken from a submission.
/// </summary>
public sealed class WorkspaceCheckpointSummaryDiagnosticsResponse
{
    public bool Enabled { get; init; }
    public string Model { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = string.Empty;
    public bool AutoSummarize { get; init; }

    /// <summary>The model server answered a request just now.</summary>
    public bool ModelReachable { get; init; }

    /// <summary>The configured model has been pulled into the model server.</summary>
    public bool ModelAvailable { get; init; }

    /// <summary>Closed, Open or HalfOpen. Kept in memory by this API instance only.</summary>
    public string Circuit { get; init; } = "Closed";

    public int ConsecutiveModelFailures { get; init; }
    public DateTime? CircuitOpenUntilUtc { get; init; }
    public DateTime? LastModelFailureUtc { get; init; }

    /// <summary>Files requested by a reviewer that are waiting (limit: <see cref="MaxRequestedQueue"/>).</summary>
    public int PendingRequested { get; init; }

    /// <summary>Files queued automatically after upload that are waiting (limit: <see cref="MaxAutomaticQueue"/>).</summary>
    public int PendingAutomatic { get; init; }

    public int Processing { get; init; }

    /// <summary>Files being processed whose lease has run out; the worker takes them again on its next poll.</summary>
    public int ExpiredLeases { get; init; }

    public int Ready { get; init; }
    public int Failed { get; init; }
    public int Cancelled { get; init; }

    /// <summary>How long the oldest waiting file has been waiting, in seconds; null when nothing waits.</summary>
    public long? OldestPendingSeconds { get; init; }

    public DateTime? LastGeneratedAtUtc { get; init; }

    /// <summary>Why files failed, most common first.</summary>
    public IReadOnlyCollection<WorkspaceSummaryFailureCount> FailuresByCode { get; init; } = Array.Empty<WorkspaceSummaryFailureCount>();

    /// <summary>What reviewers said of finished summaries, by prompt version, so a change of prompt can be judged.</summary>
    public IReadOnlyCollection<WorkspaceSummaryFeedbackCount> FeedbackByPromptVersion { get; init; } = Array.Empty<WorkspaceSummaryFeedbackCount>();

    public int MaxRequestedQueue { get; init; }
    public int MaxAutomaticQueue { get; init; }
}

public sealed class WorkspaceSummaryFailureCount
{
    public string ErrorCode { get; init; } = string.Empty;
    public int Count { get; init; }
}
