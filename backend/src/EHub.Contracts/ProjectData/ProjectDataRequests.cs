namespace EHub.Contracts.ProjectData;

public sealed class GetProjectDataRequest
{
    public string? Search { get; init; }
    public string? SubjectCode { get; init; }

    /// <summary>Semester term: SP, SU or FA. Combines with <see cref="Year"/>; either may be omitted.</summary>
    public string? Semester { get; init; }
    public int? Year { get; init; }

    // The filters below accept the query parameter more than once (?group=G1&group=G2). Values of one filter are
    // alternatives (OR); different filters still narrow each other (AND). Empty or missing means "All".
    public string[]? Group { get; init; }
    public string[]? StartupIndustry { get; init; }

    /// <summary>
    /// Effective member majors (codes from MajorCodes, or UNSPECIFIED). Matches projects where at least one active team
    /// member has any of those majors, which is the same rule the distribution counts by.
    /// </summary>
    public string[]? Major { get; init; }
    public Guid[]? LecturerId { get; init; }
    public Guid[]? MentorId { get; init; }
    public string[]? Achievement { get; init; }

    // Competition filters: a project matches when ONE of its competition entries satisfies every filter given here
    // (the names, stages and years inside one filter are alternatives).
    public string[]? Competition { get; init; }
    public string[]? CompetitionStage { get; init; }
    public int[]? CompetitionYear { get; init; }
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool IsDescending { get; init; }
}
