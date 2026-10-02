namespace EHub.Contracts.ProjectData;

public static class ProjectAchievementNames
{
    public const string Potential = "Potential";
    public const string Funded = "Funded";
    public const string Awarded = "Awarded";

    public static readonly IReadOnlyList<string> All = [Potential, Funded, Awarded];
}

public sealed class UpdateProjectAchievementsRequest
{
    public IReadOnlyCollection<string> Achievements { get; init; } = Array.Empty<string>();
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProjectAchievementsResponse
{
    public Guid ProjectId { get; init; }
    public IReadOnlyCollection<string> Achievements { get; init; } = Array.Empty<string>();
    public string RowVersion { get; init; } = string.Empty;
}
