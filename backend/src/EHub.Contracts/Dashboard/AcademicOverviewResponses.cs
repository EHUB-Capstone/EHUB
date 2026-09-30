namespace EHub.Contracts.Dashboard;

public sealed class GetAcademicOverviewRequest
{
    public Guid? SemesterId { get; init; }
    public Guid? CourseId { get; init; }
    public Guid? ClassId { get; init; }
}

public sealed class AcademicOverviewResponse
{
    public AcademicOverviewScopeResponse Scope { get; init; } = new();
    public AcademicOverviewFilterOptionsResponse FilterOptions { get; init; } = new();
    public AcademicOverviewMetricsResponse Metrics { get; init; } = new();
    public AcademicOverviewAttentionResponse Attention { get; init; } = new();
    public IReadOnlyCollection<AcademicOverviewActivityResponse> ActivityTrend { get; init; } =
        Array.Empty<AcademicOverviewActivityResponse>();
    public IReadOnlyCollection<AcademicOverviewCheckpointResponse> CheckpointProgress { get; init; } =
        Array.Empty<AcademicOverviewCheckpointResponse>();
    public IReadOnlyCollection<AcademicOverviewClassResponse> Classes { get; init; } =
        Array.Empty<AcademicOverviewClassResponse>();
    public IReadOnlyCollection<AcademicOverviewTopTeamResponse> TopTeams { get; init; } =
        Array.Empty<AcademicOverviewTopTeamResponse>();
    public DateTime LastUpdatedAtUtc { get; init; }
    public bool HasAssignedClasses { get; init; }
    public bool HasMatchingClasses { get; init; }
    public bool HasClasses { get; init; }
}

public sealed class AcademicOverviewScopeResponse
{
    public Guid SemesterId { get; init; }
    public string SemesterCode { get; init; } = string.Empty;
    public string SemesterName { get; init; } = string.Empty;
    public Guid? CourseId { get; init; }
    public string? SubjectCode { get; init; }
    public string? SubjectName { get; init; }
    public Guid? ClassId { get; init; }
    public string? ClassCode { get; init; }
}

public sealed class AcademicOverviewFilterOptionsResponse
{
    public IReadOnlyCollection<AcademicOverviewSemesterOptionResponse> Semesters { get; init; } =
        Array.Empty<AcademicOverviewSemesterOptionResponse>();
    public IReadOnlyCollection<AcademicOverviewSubjectOptionResponse> Subjects { get; init; } =
        Array.Empty<AcademicOverviewSubjectOptionResponse>();
    public IReadOnlyCollection<AcademicOverviewClassOptionResponse> Classes { get; init; } =
        Array.Empty<AcademicOverviewClassOptionResponse>();
}

public sealed class AcademicOverviewSemesterOptionResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Year { get; init; }
    public bool IsActive { get; init; }
}

public sealed class AcademicOverviewSubjectOptionResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class AcademicOverviewClassOptionResponse
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid CourseId { get; init; }
}

public sealed class AcademicOverviewMetricsResponse
{
    public int TotalClasses { get; init; }
    public int TotalTeams { get; init; }
    public int TotalProjects { get; init; }
    public int TotalSubmissions { get; init; }
    public int TotalEvaluations { get; init; }
    public int TotalPotentialProjects { get; init; }
}

public sealed class AcademicOverviewAttentionResponse
{
    public int MissedDeadlines { get; init; }
    public int PendingEvaluations { get; init; }
}

public sealed class AcademicOverviewActivityResponse
{
    public DateTime WeekStartUtc { get; init; }
    public int Submissions { get; init; }
    public int Evaluations { get; init; }
}

public sealed class AcademicOverviewCheckpointResponse
{
    public Guid CheckpointId { get; init; }
    public string CourseCode { get; init; } = string.Empty;
    public int CheckpointNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public int ExpectedTeams { get; init; }
    public int SubmittedTeams { get; init; }
    public int EvaluatedProjects { get; init; }
    public int MissedDeadlineTeams { get; init; }
}

public sealed class AcademicOverviewClassResponse
{
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public int Teams { get; init; }
    public int Projects { get; init; }
    public int Submissions { get; init; }
    public int Evaluations { get; init; }
    public int PotentialProjects { get; init; }
}

public sealed class AcademicOverviewTopTeamResponse
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public decimal CourseTotal { get; init; }
    public int CompletedComponents { get; init; }
    public int TotalComponents { get; init; }
}
