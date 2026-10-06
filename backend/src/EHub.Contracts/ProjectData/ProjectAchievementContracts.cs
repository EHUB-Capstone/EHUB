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

    /// <summary>Optional reason for the labels (up to 500 characters); only allowed together with at least one label.</summary>
    public string? Note { get; init; }
}

public sealed class ProjectAchievementHistoryItemResponse
{
    public Guid Id { get; init; }

    /// <summary>PROJECT_ACHIEVEMENTS_CHANGED or ACHIEVEMENTS_CARRIED_OVER.</summary>
    public string Action { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string? ActorName { get; init; }
    public DateTime OccurredAtUtc { get; init; }

    // What the change did, so the client can show it without reading the summary sentence.
    // All empty (and NoteChanged false) means the entry could not be read; show Summary instead.
    public IReadOnlyCollection<string> Added { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Removed { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Kept { get; init; } = Array.Empty<string>();

    /// <summary>True when this entry set or cleared the note.</summary>
    public bool NoteChanged { get; init; }

    /// <summary>The note set by this entry; null when it was cleared (or the entry did not touch it).</summary>
    public string? Note { get; init; }
}

public sealed class ProjectAchievementHistoryResponse
{
    /// <summary>All recorded changes; <see cref="Items"/> holds only the latest ones, newest first.</summary>
    public int TotalCount { get; init; }
    public IReadOnlyCollection<ProjectAchievementHistoryItemResponse> Items { get; init; } = Array.Empty<ProjectAchievementHistoryItemResponse>();
}

public sealed class ProjectAchievementsResponse
{
    public Guid ProjectId { get; init; }
    public IReadOnlyCollection<string> Achievements { get; init; } = Array.Empty<string>();
    public string RowVersion { get; init; } = string.Empty;
    public string? Note { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public ProjectDataPersonResponse? UpdatedBy { get; init; }
}
