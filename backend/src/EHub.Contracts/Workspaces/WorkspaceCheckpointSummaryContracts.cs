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

    /// <summary>Checkpoint requirements and rubric criteria combined from the files read so far, without another model call.</summary>
    public IReadOnlyCollection<WorkspaceCheckpointRequirementFinding> Requirements { get; init; } = Array.Empty<WorkspaceCheckpointRequirementFinding>();

    /// <summary>Figures that two files state differently (a sample of 51 and of 52), found without the model.</summary>
    public IReadOnlyCollection<WorkspaceFigureConflict> Conflicts { get; init; } = Array.Empty<WorkspaceFigureConflict>();

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

    /// <summary>Queued automatically after upload; a reviewer's request moves it to the front.</summary>
    public bool IsAutomatic { get; init; }

    /// <summary>Quick view built without the model, shown while the AI summary is not ready yet.</summary>
    public WorkspaceFilePreview? Preview { get; init; }

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

    /// <summary>Figures the file states twice with different values, found without the model.</summary>
    public IReadOnlyCollection<WorkspaceFigureConflict> Conflicts { get; init; } = Array.Empty<WorkspaceFigureConflict>();

    /// <summary>Lines of the file that report a status (done, in progress, pending), found without the model.</summary>
    public IReadOnlyCollection<WorkspaceStatusItem> StatusItems { get; init; } = Array.Empty<WorkspaceStatusItem>();

    /// <summary>Sentences that state a percentage or a sample size, kept to compare the files of a checkpoint.</summary>
    public IReadOnlyCollection<string> Figures { get; init; } = Array.Empty<string>();

    /// <summary>Sentences addressed to the AI (orders, requests for a grade) that were left out before summarising.</summary>
    public int IgnoredInstructions { get; init; }

    /// <summary>The file is mostly images or diagrams; only its titles and few lines of text were read.</summary>
    public bool LittleText { get; init; }
}

/// <summary>A line of the file that reports the status of a piece of work, quoted as written.</summary>
public sealed class WorkspaceStatusItem
{
    /// <summary>Done, InProgress or Pending.</summary>
    public string Status { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;

    /// <summary>The numbered request this status answers, when the file numbers both ("1.Thêm…" / "1. PENDING: …").</summary>
    public string? Request { get; init; }
}

/// <summary>One measure (for example the conversion rate) given different values in different places of a file.</summary>
public sealed class WorkspaceFigureConflict
{
    public string Subject { get; init; } = string.Empty;
    public IReadOnlyCollection<WorkspaceFigureValue> Values { get; init; } = Array.Empty<WorkspaceFigureValue>();
}

public sealed class WorkspaceFigureValue
{
    public string Value { get; init; } = string.Empty;

    /// <summary>The sentence of the file that states the value, quoted as written.</summary>
    public string Quote { get; init; } = string.Empty;

    /// <summary>The file that states it, when figures of different files are compared.</summary>
    public string? Source { get; init; }
}

/// <summary>Key sentences and checklist evidence taken from the file without the model.</summary>
public sealed class WorkspaceFilePreview
{
    public IReadOnlyCollection<string> Highlights { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<WorkspaceFileRequirementFinding> Requirements { get; init; } = Array.Empty<WorkspaceFileRequirementFinding>();

    /// <summary>Figures the file states twice with different values.</summary>
    public IReadOnlyCollection<WorkspaceFigureConflict> Conflicts { get; init; } = Array.Empty<WorkspaceFigureConflict>();

    /// <summary>Lines of the file that report a status (done, in progress, pending).</summary>
    public IReadOnlyCollection<WorkspaceStatusItem> StatusItems { get; init; } = Array.Empty<WorkspaceStatusItem>();

    /// <summary>Sentences that state a percentage or a sample size, kept to compare the files of a checkpoint.</summary>
    public IReadOnlyCollection<string> Figures { get; init; } = Array.Empty<string>();

    /// <summary>Sentences addressed to the AI (orders, requests for a grade) that were left out before summarising.</summary>
    public int IgnoredInstructions { get; init; }

    /// <summary>The file is mostly images or diagrams; only its titles and few lines of text were read.</summary>
    public bool LittleText { get; init; }
}

public sealed class WorkspaceFileRequirementFinding
{
    public int Index { get; init; }

    /// <summary>Requirement (from the checkpoint) or Criterion (from its rubric).</summary>
    public string Kind { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Related (a sentence in the file is about the item, matched by its words or topic and not negated) or NotFound.
    /// Never a pass/fail judgement.
    /// </summary>
    public string Finding { get; init; } = string.Empty;

    /// <summary>A sentence quoted from the file; null for NotFound and Unclear.</summary>
    public string? Evidence { get; init; }

    /// <summary>Share of the checkpoint grade (percent), for a rubric criterion.</summary>
    public decimal? Weight { get; init; }

    /// <summary>
    /// Document (a file can show it), Product (judged on the product or demo) or Live (presentation, Q&amp;A, teamwork:
    /// no file can show it). Live items are never searched, so they are not reported as missing.
    /// </summary>
    public string Assessment { get; init; } = "Document";

    /// <summary>What the Excellent level of the criterion expects, and which of it the file mentions.</summary>
    public IReadOnlyCollection<WorkspaceCriterionCue> Cues { get; init; } = Array.Empty<WorkspaceCriterionCue>();
}

/// <summary>One thing the criterion expects (roadmap, persona...), and whether the file mentions it.</summary>
public sealed class WorkspaceCriterionCue
{
    public string Label { get; init; } = string.Empty;
    public bool Found { get; init; }

    /// <summary>A sentence of the file that mentions it; null when not found.</summary>
    public string? Evidence { get; init; }
}

public sealed class WorkspaceCheckpointRequirementFinding
{
    public int Index { get; init; }

    /// <summary>Requirement (from the checkpoint) or Criterion (from its rubric).</summary>
    public string Kind { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    /// <summary>Related, NotFound, NotChecked (no file read yet), Live or Product (see <see cref="Assessment"/>).</summary>
    public string Finding { get; init; } = string.Empty;

    public string? Evidence { get; init; }

    /// <summary>The file the evidence was quoted from.</summary>
    public string? Source { get; init; }

    /// <summary>Share of the checkpoint grade (percent), for a rubric criterion.</summary>
    public decimal? Weight { get; init; }

    /// <summary>
    /// Document (a file can show it), Product (judged on the product or demo) or Live (presentation, Q&amp;A, teamwork:
    /// no file can show it). Live items are never searched, so they are not reported as missing.
    /// </summary>
    public string Assessment { get; init; } = "Document";

    /// <summary>What the Excellent level of the criterion expects, and which of it the file mentions.</summary>
    public IReadOnlyCollection<WorkspaceCriterionCue> Cues { get; init; } = Array.Empty<WorkspaceCriterionCue>();
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
