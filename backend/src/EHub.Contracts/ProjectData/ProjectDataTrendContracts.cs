namespace EHub.Contracts.ProjectData;

/// <summary>
/// One row of the cross-semester trend: a semester, or a semester within one subject or one class.
/// The average score is the official course score (0-10) of the groups whose every graded component is published.
/// </summary>
public sealed class ProjectDataTrendRowResponse
{
    public string SemesterCode { get; init; } = string.Empty;
    public int Year { get; init; }

    /// <summary>SP, SU or FA.</summary>
    public string Term { get; init; } = string.Empty;
    public string? SubjectCode { get; init; }
    public Guid? ClassId { get; init; }
    public string? ClassCode { get; init; }

    public int TotalGroups { get; init; }

    /// <summary>Groups whose course score is official: every weighted component has a published evaluation.</summary>
    public int GradedGroups { get; init; }

    /// <summary>Null when no group in the row is graded yet.</summary>
    public decimal? AverageScore { get; init; }
    public int PotentialGroups { get; init; }
    public int InternallyFundedGroups { get; init; }
    public int ExternallyFundedGroups { get; init; }
    public int AwardedGroups { get; init; }

    /// <summary>
    /// Change since the previous semester of the same subject (or class code). Null for the earliest semester, or
    /// when either semester has no graded group, for the score.
    /// </summary>
    public decimal? AverageScoreChange { get; init; }
    public int? PotentialChange { get; init; }
    public int? InternallyFundedChange { get; init; }
    public int? ExternallyFundedChange { get; init; }
    public int? AwardedChange { get; init; }
}

public sealed class ProjectDataTrendsResponse
{
    /// <summary>One row per semester, oldest first.</summary>
    public IReadOnlyCollection<ProjectDataTrendRowResponse> Semesters { get; init; } = Array.Empty<ProjectDataTrendRowResponse>();

    /// <summary>One row per semester and subject, oldest first.</summary>
    public IReadOnlyCollection<ProjectDataTrendRowResponse> Subjects { get; init; } = Array.Empty<ProjectDataTrendRowResponse>();

    /// <summary>One row per class, oldest semester first.</summary>
    public IReadOnlyCollection<ProjectDataTrendRowResponse> Classes { get; init; } = Array.Empty<ProjectDataTrendRowResponse>();
}
