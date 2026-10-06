namespace EHub.Contracts.Classes;

public static class GroupProjectWarningTypes
{
    public const string GroupHasMultipleProjects = "GROUP_HAS_MULTIPLE_PROJECTS";
    public const string ProjectHasMultipleGroups = "PROJECT_HAS_MULTIPLE_GROUPS";
}

/// <summary>
/// One Group/Project inconsistency. <see cref="Subject"/> is the Group (or Project) that breaks the
/// 1-1 rule and <see cref="Related"/> lists the several Projects (or Groups) it is assigned to.
/// </summary>
public sealed class GroupProjectWarningDto
{
    public string Type { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Related { get; init; } = Array.Empty<string>();
    public string Message { get; init; } = string.Empty;
}

public sealed class GroupProjectConsistencyResponse
{
    public bool IsConsistent { get; init; } = true;
    public IReadOnlyCollection<GroupProjectWarningDto> Warnings { get; init; } = Array.Empty<GroupProjectWarningDto>();
}
