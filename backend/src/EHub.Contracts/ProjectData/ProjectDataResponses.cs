namespace EHub.Contracts.ProjectData;

public sealed class ProjectDataPersonResponse
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
}

public sealed class ProjectDataMentorResponse
{
    public Guid AssignmentId { get; init; }
    public Guid MentorProfileId { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Slot { get; init; } = string.Empty;
    public DateTime AssignedAtUtc { get; init; }
    public DateTime? EndedAtUtc { get; init; }

    /// <summary>True when the assignment was closed by class completion rather than being currently active.</summary>
    public bool IsHistorical { get; init; }
}

public sealed class ProjectDataItemResponse
{
    public Guid ProjectId { get; init; }
    public Guid TeamId { get; init; }
    public Guid ClassId { get; init; }
    public Guid SemesterId { get; init; }
    public string SemesterCode { get; init; } = string.Empty;
    public Guid SubjectId { get; init; }
    public string SubjectCode { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Groups { get; init; } = Array.Empty<string>();
    public string ProjectName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyCollection<string> StartupIndustries { get; init; } = Array.Empty<string>();
    public ProjectDataPersonResponse? Lecturer { get; init; }
    public ProjectDataMentorResponse? Mentor { get; init; }
    public ProjectDataMentorResponse? AcademicMentor { get; init; }
    public IReadOnlyCollection<string> Achievements { get; init; } = Array.Empty<string>();
    public string? AchievementNote { get; init; }
    public DateTime? AchievementsUpdatedAtUtc { get; init; }
    public ProjectDataPersonResponse? AchievementsUpdatedBy { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProjectDataSummaryResponse
{
    /// <summary>Groups (one project per team) matching the current scope, search and filters.</summary>
    public int TotalGroups { get; init; }
    public int PotentialGroups { get; init; }
    public int FundedGroups { get; init; }
    public int AwardedGroups { get; init; }
}

public sealed class ProjectDataSubjectOptionResponse
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class ProjectDataMentorOptionResponse
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Slot { get; init; } = string.Empty;
}

public sealed class ProjectDataFilterOptionsResponse
{
    public IReadOnlyCollection<ProjectDataSubjectOptionResponse> Subjects { get; init; } = Array.Empty<ProjectDataSubjectOptionResponse>();
    public IReadOnlyCollection<int> Years { get; init; } = Array.Empty<int>();
    public IReadOnlyCollection<string> Groups { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> StartupIndustries { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<ProjectDataPersonResponse> Lecturers { get; init; } = Array.Empty<ProjectDataPersonResponse>();
    public IReadOnlyCollection<ProjectDataMentorOptionResponse> Mentors { get; init; } = Array.Empty<ProjectDataMentorOptionResponse>();
    public IReadOnlyCollection<string> Achievements { get; init; } = ProjectAchievementNames.All;
}
