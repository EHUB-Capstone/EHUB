namespace EHub.Contracts.Workspaces;

/// <summary>AI summary of a team's current checkpoint files and links. Returned to admins, lecturers and mentors only.</summary>
public sealed class WorkspaceCheckpointSummaryResponse
{
    public int CheckpointNumber { get; init; }

    /// <summary>Disabled, None, Pending, Processing, Ready or Failed.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>True when the team changed its files or links after this summary was generated.</summary>
    public bool IsStale { get; init; }

    /// <summary>True when the documents exceeded the input limit and only their start was summarised.</summary>
    public bool InputTruncated { get; init; }

    public int MaxInputCharacters { get; init; }
    public DateTime? RequestedAt { get; init; }
    public DateTime? GeneratedAt { get; init; }
    public string? Model { get; init; }
    public string? ErrorCode { get; init; }
    public WorkspaceCheckpointSummaryContent? Content { get; init; }
}

public sealed class WorkspaceCheckpointSummaryContent
{
    public string Overview { get; init; } = string.Empty;
    public IReadOnlyCollection<string> KeyPoints { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<WorkspaceCheckpointSummaryFile> Files { get; init; } = Array.Empty<WorkspaceCheckpointSummaryFile>();
    public IReadOnlyCollection<WorkspaceCheckpointSummaryLink> Links { get; init; } = Array.Empty<WorkspaceCheckpointSummaryLink>();
    public IReadOnlyCollection<WorkspaceCheckpointSummaryRequirement> Requirements { get; init; } = Array.Empty<WorkspaceCheckpointSummaryRequirement>();
    public IReadOnlyCollection<string> Questions { get; init; } = Array.Empty<string>();
}

public sealed class WorkspaceCheckpointSummaryFile
{
    public Guid FileId { get; init; }
    public string Name { get; init; } = string.Empty;

    /// <summary>Summarized, Unreadable (no text could be read) or NotIncluded (beyond the input limit).</summary>
    public string Status { get; init; } = string.Empty;

    public string? Summary { get; init; }
}

/// <summary>Links are listed by name and domain only; their content is never fetched.</summary>
public sealed class WorkspaceCheckpointSummaryLink
{
    public string Name { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
}

public sealed class WorkspaceCheckpointSummaryRequirement
{
    public int Index { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Related (a quote from the documents that names the requirement), NotFound (the model found nothing) or Unclear
    /// (the model claimed coverage but its quote is not in the documents, or does not mention the requirement).
    /// Never a pass/fail judgement.
    /// </summary>
    public string Finding { get; init; } = string.Empty;

    /// <summary>A quote found in the documents; null for NotFound and for quotes the model invented.</summary>
    public string? Evidence { get; init; }
}
