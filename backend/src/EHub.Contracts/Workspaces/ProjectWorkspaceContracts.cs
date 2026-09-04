namespace EHub.Contracts.Workspaces;

public sealed class CreateProjectWorkspaceRequest
{
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StartupField { get; init; } = string.Empty;
    public IReadOnlyCollection<string> TechnologyStack { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Keywords { get; init; } = Array.Empty<string>();
}

public sealed class UpdateProjectWorkspaceRequest
{
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StartupField { get; init; } = string.Empty;
    public IReadOnlyCollection<string> TechnologyStack { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Keywords { get; init; } = Array.Empty<string>();
}

public sealed class ProjectWorkspaceDto
{
    public Guid Id { get; init; }
    public Guid TeamId { get; init; }
    public Guid ClassId { get; init; }
    public Guid SubjectId { get; init; }
    public Guid SemesterId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StartupField { get; init; } = string.Empty;
    public IReadOnlyCollection<string> TechnologyStack { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Keywords { get; init; } = Array.Empty<string>();
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed class ProjectActivityDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public Guid? ActorUserId { get; init; }
    public string ActorName { get; init; } = "System";
    public IReadOnlyCollection<string> ChangedFields { get; init; } = Array.Empty<string>();
    public DateTime OccurredAtUtc { get; init; }
}

public sealed class WorkspaceTeamDto
{
    public Guid Id { get; init; }
    public string TeamCode { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? LeaderId { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class WorkspaceClassDto
{
    public Guid Id { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public Guid SubjectId { get; init; }
    public string SubjectCode { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid SemesterId { get; init; }
    public string SemesterCode { get; init; } = string.Empty;
}

public sealed class WorkspaceMemberDto
{
    public Guid StudentId { get; init; }
    public Guid? UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string RollNumber { get; init; } = string.Empty;
    public string MajorCode { get; init; } = string.Empty;
    public string RoleInTeam { get; init; } = string.Empty;
}

public sealed class WorkspacePersonDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public sealed class ProjectWorkspaceDetailDto
{
    public WorkspaceTeamDto Team { get; init; } = new();
    public WorkspaceClassDto Class { get; init; } = new();
    public IReadOnlyCollection<WorkspaceMemberDto> Members { get; init; } = Array.Empty<WorkspaceMemberDto>();
    public WorkspacePersonDto? Lecturer { get; init; }
    public WorkspacePersonDto? Mentor { get; init; }
    public ProjectWorkspaceDto? Project { get; init; }
    public IReadOnlyCollection<ProjectActivityDto> Activities { get; init; } = Array.Empty<ProjectActivityDto>();
}

public sealed class WorkspaceOptionDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public Guid ClassId { get; init; }
    public string ClassCode { get; init; } = string.Empty;
    public string CourseCode { get; init; } = string.Empty;
    public string Semester { get; init; } = string.Empty;
    public string AccessMode { get; init; } = "READ_WRITE";
    public bool IsArchived { get; init; }
    public bool IsCurrent { get; init; }
    public bool HasWorkspace { get; init; }
}

public sealed class WorkspaceContextDto
{
    public WorkspaceOptionDto SelectedWorkspace { get; init; } = new();
    public IReadOnlyCollection<WorkspaceOptionDto> AvailableWorkspaces { get; init; } = Array.Empty<WorkspaceOptionDto>();
    public string AccessMode { get; init; } = "READ_WRITE";
}
