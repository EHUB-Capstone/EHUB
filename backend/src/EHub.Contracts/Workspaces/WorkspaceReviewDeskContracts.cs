namespace EHub.Contracts.Workspaces;

/// <summary>What a reviewer (admin, lecturer or mentor) sees first: a short list of teams that need a look today. Staff only.</summary>
public sealed class WorkspaceReviewDeskResponse
{
    /// <summary>Teams that need a look, most urgent first.</summary>
    public IReadOnlyCollection<ReviewDeskInboxItemDto> Inbox { get; init; } = Array.Empty<ReviewDeskInboxItemDto>();
}

public sealed class ReviewDeskInboxItemDto
{
    public Guid TeamId { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public int CheckpointNumber { get; init; }

    /// <summary>NewVersion (a version this person has not opened), Overdue (not submitted after the deadline) or DueSoon.</summary>
    public string Reason { get; init; } = string.Empty;

    public int? VersionNumber { get; init; }

    /// <summary>When the new version was submitted (NewVersion).</summary>
    public DateTime? SubmittedAtUtc { get; init; }

    /// <summary>The checkpoint deadline (Overdue, DueSoon).</summary>
    public DateTime? DueAtUtc { get; init; }
}

/// <summary>Whether AI summaries, and with them the review desk, exist in this environment.</summary>
public sealed class WorkspaceAiSummaryStatusResponse
{
    public bool Enabled { get; init; }
}
