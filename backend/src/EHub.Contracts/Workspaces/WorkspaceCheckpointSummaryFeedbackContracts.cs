namespace EHub.Contracts.Workspaces;

/// <summary>A reviewer's verdict on an AI file summary. <see cref="Reason"/> is one of a fixed list and only goes with NotHelpful.</summary>
public sealed class SetCheckpointSummaryFeedbackRequest
{
    /// <summary>The generation shown to the reviewer. Optional during rollout of generation-aware clients.</summary>
    public Guid? GenerationId { get; init; }
    /// <summary>Helpful or NotHelpful.</summary>
    public string Rating { get; init; } = string.Empty;

    /// <summary>Inaccurate, MissingKeyPoint, WrongEvidence or Other; null when no reason is given.</summary>
    public string? Reason { get; init; }
}

/// <summary>The signed-in reviewer's own verdict on a file summary. Other reviewers' verdicts are never returned.</summary>
public sealed class WorkspaceSummaryFeedback
{
    public string Rating { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public sealed class WorkspaceSummaryFeedbackCount
{
    public string PromptVersion { get; init; } = string.Empty;
    public int Helpful { get; init; }
    public int NotHelpful { get; init; }
}
