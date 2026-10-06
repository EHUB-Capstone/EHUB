namespace EHub.Contracts.Teams;

public static class TeamContinuationOutcomes
{
    public const string Created = "Created";
    public const string MembersAdded = "MembersAdded";
    public const string NoChange = "NoChange";
    public const string NotEligible = "NotEligible";
    public const string Dissolved = "Dissolved";
    public const string ContinuedElsewhere = "ContinuedElsewhere";
}

public sealed class TeamContinuationItemDto
{
    public Guid? TeamId { get; init; }
    public Guid SourceTeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string SourceTeamName { get; init; } = string.Empty;
    public string SourceClassCode { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public IReadOnlyCollection<string> Reasons { get; init; } = Array.Empty<string>();
}

public sealed class TeamContinuationSummaryDto
{
    public string? SourceSemesterCode { get; init; }
    public int CreatedCount { get; init; }
    public int MembersAddedCount { get; init; }
    public int NotEligibleCount { get; init; }
    public IReadOnlyCollection<TeamContinuationItemDto> Items { get; init; } = Array.Empty<TeamContinuationItemDto>();
}

public sealed class TeamLineageMemberDto
{
    public Guid StudentId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? RollNumber { get; init; }
    public bool IsLeader { get; init; }
}

public sealed class TeamLineageTermDto
{
    public Guid TeamId { get; init; }
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public Guid SemesterId { get; init; }
    public string SemesterCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string? ProjectName { get; init; }
    public string? ProjectStatus { get; init; }
    public bool IsCurrent { get; init; }
    public bool CanViewSubmissions { get; init; }
    public bool CanViewScores { get; init; }
    public IReadOnlyCollection<TeamLineageMemberDto> Members { get; init; } = Array.Empty<TeamLineageMemberDto>();
}

public sealed class TeamLineageDto
{
    public Guid TeamLineageId { get; init; }
    public IReadOnlyCollection<TeamLineageTermDto> Terms { get; init; } = Array.Empty<TeamLineageTermDto>();
}

public sealed class TeamLineageEvaluationDto
{
    public Guid Id { get; init; }
    public string EvaluatorRole { get; init; } = string.Empty;
    public decimal TotalScore { get; init; }
    public decimal MaxTotalScore { get; init; }
    public string? OverallFeedback { get; init; }
}

public sealed class TeamLineageSubmissionDto
{
    public Guid Id { get; init; }
    public Guid CheckpointId { get; init; }
    public string CheckpointName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int VersionNumber { get; init; }
    public DateTime? SubmittedAt { get; init; }
    // Null when the caller is not allowed to see scores of this term.
    public IReadOnlyCollection<TeamLineageEvaluationDto>? Evaluations { get; init; }
}

public sealed class TeamContinuityReportItemDto
{
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public int ContinuedTeamCount { get; init; }
    public int DissolvedCount { get; init; }
}

public sealed class TeamContinuityReportDto
{
    public Guid SemesterId { get; init; }
    public string SemesterCode { get; init; } = string.Empty;
    public int ContinuedTeamCount { get; init; }
    public int ContinuedProjectCount { get; init; }
    public int DissolvedCount { get; init; }
    public IReadOnlyCollection<TeamContinuityReportItemDto> Classes { get; init; } = Array.Empty<TeamContinuityReportItemDto>();
}
