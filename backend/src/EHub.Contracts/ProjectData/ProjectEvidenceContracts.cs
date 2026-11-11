namespace EHub.Contracts.ProjectData;

/// <summary>How far a graded part of the course score has come for one project.</summary>
public static class ProjectEvidenceStatuses
{
    /// <summary>The newest graded version is published, so its score and feedback count.</summary>
    public const string Published = "Published";

    /// <summary>Graded and submitted, but not published yet. Staff see that it exists, never its score.</summary>
    public const string PendingPublication = "PendingPublication";

    /// <summary>No submitted evaluation yet.</summary>
    public const string NotGraded = "NotGraded";
}

public static class ProjectEvidenceKinds
{
    public const string Checkpoint = "Checkpoint";
    public const string Assessment = "Assessment";
}

/// <summary>One weighted part of the course score: a checkpoint, or a course assessment outside the checkpoints.</summary>
public sealed class ProjectEvidenceComponentResponse
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Checkpoint or Assessment.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>The checkpoint order (1, 2, ...); null for an assessment.</summary>
    public int? CheckpointNumber { get; init; }

    /// <summary>Share of the course score, 0-100.</summary>
    public decimal WeightPercent { get; init; }

    /// <summary>Published, PendingPublication or NotGraded.</summary>
    public string Status { get; init; } = ProjectEvidenceStatuses.NotGraded;

    /// <summary>The 0-10 score; only set when the status is Published.</summary>
    public decimal? Score { get; init; }

    /// <summary>The lecturer's overall feedback; only set when the status is Published.</summary>
    public string? Feedback { get; init; }

    public DateTime? PublishedAtUtc { get; init; }
}

public sealed class ProjectEvidenceEvaluationResponse
{
    /// <summary>The official 0-10 course score, the same number the trends and the export use; null until every part is published.</summary>
    public decimal? CourseScore { get; init; }

    /// <summary>Checkpoints first (by order), then the other assessments. Empty when the course has no weighted parts.</summary>
    public IReadOnlyCollection<ProjectEvidenceComponentResponse> Components { get; init; } = Array.Empty<ProjectEvidenceComponentResponse>();
}

/// <summary>A mentor of the team and the work done in that assignment. Contact details are never part of it.</summary>
public sealed class ProjectEvidenceMentorResponse
{
    public Guid AssignmentId { get; init; }
    public Guid MentorProfileId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Organization { get; init; }

    /// <summary>Enterprise or Academic, the mentor slot of the team.</summary>
    public string Slot { get; init; } = string.Empty;

    public DateTime AssignedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }

    /// <summary>True for the mentor shown on the project list; false for a mentor who left or was replaced earlier.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Expertise, startup domains and technology skills together, without duplicates.</summary>
    public IReadOnlyCollection<string> Expertise { get; init; } = Array.Empty<string>();

    /// <summary>Completed mentoring sessions held in this assignment.</summary>
    public int CompletedSessions { get; init; }
}

public sealed class ProjectEvidenceMentoringResponse
{
    /// <summary>Current mentors first, then earlier ones, newest first. Empty while the team never had a mentor.</summary>
    public IReadOnlyCollection<ProjectEvidenceMentorResponse> Mentors { get; init; } = Array.Empty<ProjectEvidenceMentorResponse>();

    public int TotalCompletedSessions { get; init; }
    public DateTime? LastSessionAtUtc { get; init; }
}

/// <summary>The academic evidence of one project: how it was graded and who mentored it.</summary>
public sealed class ProjectEvidenceResponse
{
    public Guid ProjectId { get; init; }
    public ProjectEvidenceEvaluationResponse Evaluation { get; init; } = new();
    public ProjectEvidenceMentoringResponse Mentoring { get; init; } = new();
}
