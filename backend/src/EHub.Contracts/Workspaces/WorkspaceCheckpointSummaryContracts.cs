namespace EHub.Contracts.Workspaces;

/// <summary>AI summaries of a team's checkpoint files, one per file version. Returned to admins, lecturers and mentors only.</summary>
public sealed class WorkspaceCheckpointSummaryResponse
{
    public int CheckpointNumber { get; init; }

    /// <summary>False when AI summaries are switched off on this server.</summary>
    public bool Enabled { get; init; }

    /// <summary>Characters of each file the model reads; longer files are condensed to their most informative sentences.</summary>
    public int FilePromptCharacters { get; init; }

    /// <summary>The files currently shown on the checkpoint, newest version first, each with its summary state.</summary>
    public IReadOnlyCollection<WorkspaceFileSummary> Files { get; init; } = Array.Empty<WorkspaceFileSummary>();

    /// <summary>Checkpoint requirements combined from the files summarised so far, without another model call.</summary>
    public IReadOnlyCollection<WorkspaceCheckpointRequirementFinding> Requirements { get; init; } = Array.Empty<WorkspaceCheckpointRequirementFinding>();

    /// <summary>Links are listed by name and domain only; their content is never fetched.</summary>
    public IReadOnlyCollection<WorkspaceCheckpointSummaryLink> Links { get; init; } = Array.Empty<WorkspaceCheckpointSummaryLink>();
}

public sealed class WorkspaceFileSummary
{
    public Guid FileId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int VersionNumber { get; init; }
    public DateTime UploadedAt { get; init; }

    /// <summary>None, Pending, Processing, Ready, Failed or Cancelled.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>For Pending files: how many summaries are ahead in the shared queue.</summary>
    public int? QueueAhead { get; init; }

    public DateTime? RequestedAt { get; init; }
    public DateTime? GeneratedAt { get; init; }
    public string? ErrorCode { get; init; }

    /// <summary>True when the file was long and only its most informative sentences were read.</summary>
    public bool InputTruncated { get; init; }

    public WorkspaceFileSummaryContent? Content { get; init; }
}

public sealed class WorkspaceFileSummaryContent
{
    public string Overview { get; init; } = string.Empty;
    public IReadOnlyCollection<string> KeyPoints { get; init; } = Array.Empty<string>();

    /// <summary>The file's most informative sentences, quoted as written and chosen without the model.</summary>
    public IReadOnlyCollection<string> Highlights { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<WorkspaceFileRequirementFinding> Requirements { get; init; } = Array.Empty<WorkspaceFileRequirementFinding>();
    public IReadOnlyCollection<string> Questions { get; init; } = Array.Empty<string>();
}

public sealed class WorkspaceFileRequirementFinding
{
    public int Index { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Related (a sentence in the file names the requirement), NotFound (the model found nothing) or Unclear (the
    /// model claimed coverage but no sentence names the requirement). Never a pass/fail judgement.
    /// </summary>
    public string Finding { get; init; } = string.Empty;

    /// <summary>A sentence quoted from the file; null for NotFound and Unclear.</summary>
    public string? Evidence { get; init; }
}

public sealed class WorkspaceCheckpointRequirementFinding
{
    public int Index { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>Related, Unclear, NotFound, or NotChecked when no file has been summarised yet.</summary>
    public string Finding { get; init; } = string.Empty;

    public string? Evidence { get; init; }

    /// <summary>The file the evidence was quoted from.</summary>
    public string? Source { get; init; }
}

public sealed class WorkspaceCheckpointSummaryLink
{
    public string Name { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
}

/// <summary>Files to summarise. Files that already have a current summary or are queued are skipped.</summary>
public sealed class RequestCheckpointSummaryRequest
{
    public IReadOnlyCollection<Guid> FileIds { get; init; } = Array.Empty<Guid>();
}
