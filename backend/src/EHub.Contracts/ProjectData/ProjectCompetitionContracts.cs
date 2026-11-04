namespace EHub.Contracts.ProjectData;

/// <summary>The competitions projects usually enter, plus "Others" for anything else (named by the user).</summary>
public static class ProjectCompetitionNames
{
    public const string Others = "Others";

    public static readonly IReadOnlyList<string> All =
    [
        "SV.STARTUP",
        "Sinh viên Thế Hệ Mới VTV",
        "VietFuture Awards",
        "FTalent",
        "DBC",
        "FShark",
        "Startup Wheel",
        "DAVAS",
        "SURF",
        Others,
    ];

    public const int OtherNameMaxLength = 100;

    /// <summary>The year a competition took place, not the academic year of the project.</summary>
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public const int MaxEntriesPerProject = 30;
}

/// <summary>Furthest stage a project reached. The values are the enum names and are sent as text.</summary>
public static class ProjectCompetitionStageNames
{
    public const string Registered = "Registered";
    public const string Qualifier = "Qualifier";
    public const string Final = "Final";

    public static readonly IReadOnlyList<string> All = [Registered, Qualifier, Final];
}

public sealed class ProjectCompetitionEntryRequest
{
    /// <summary>A name from <see cref="ProjectCompetitionNames.All"/>.</summary>
    public string Competition { get; init; } = string.Empty;

    /// <summary>Required (and only used) when <see cref="Competition"/> is "Others".</summary>
    public string? OtherName { get; init; }
    public int Year { get; init; }

    /// <summary>Registered, Qualifier or Final.</summary>
    public string Stage { get; init; } = string.Empty;
}

/// <summary>Replaces the whole list of competitions of a project.</summary>
public sealed class UpdateProjectCompetitionsRequest
{
    public IReadOnlyCollection<ProjectCompetitionEntryRequest> Entries { get; init; } = Array.Empty<ProjectCompetitionEntryRequest>();
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProjectCompetitionResponse
{
    public string Competition { get; init; } = string.Empty;
    public string? OtherName { get; init; }

    /// <summary>What to show: the free text for "Others", otherwise the competition name.</summary>
    public string Name { get; init; } = string.Empty;
    public int Year { get; init; }
    public string Stage { get; init; } = string.Empty;
}

public sealed class ProjectCompetitionsResponse
{
    public Guid ProjectId { get; init; }
    public IReadOnlyCollection<ProjectCompetitionResponse> Competitions { get; init; } = Array.Empty<ProjectCompetitionResponse>();
    public string RowVersion { get; init; } = string.Empty;
}
