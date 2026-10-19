namespace EHub.Contracts.ProjectData;

public static class ProjectStartupIndustryRules
{
    public const int MinIndustries = 1;
    public const int MaxIndustries = 3;
}

public sealed class UpdateProjectStartupIndustriesRequest
{
    /// <summary>Ids of the active startup industries the project should have (1 to 3).</summary>
    public IReadOnlyCollection<Guid> StartupIndustryIds { get; init; } = Array.Empty<Guid>();
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProjectStartupIndustriesResponse
{
    public Guid ProjectId { get; init; }
    public IReadOnlyCollection<string> StartupIndustries { get; init; } = Array.Empty<string>();
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProjectStartupIndustryHistoryItemResponse
{
    public Guid Id { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string? ActorName { get; init; }
    public DateTime OccurredAtUtc { get; init; }

    // All three empty means the entry could not be read; show Summary instead.
    public IReadOnlyCollection<string> Added { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Removed { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Kept { get; init; } = Array.Empty<string>();
}

public sealed class ProjectStartupIndustryHistoryResponse
{
    /// <summary>All recorded changes; <see cref="Items"/> holds only the latest ones, newest first.</summary>
    public int TotalCount { get; init; }
    public IReadOnlyCollection<ProjectStartupIndustryHistoryItemResponse> Items { get; init; } = Array.Empty<ProjectStartupIndustryHistoryItemResponse>();
}
