namespace EHub.Contracts.Showcase;

public sealed class ShowcaseLinkRequest
{
    public string LinkType { get; init; } = "Website";
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

/// <summary>
/// Create/update payload for the showcase-only content. Project name, description, problem, solution and target users
/// belong to the Project Profile and are edited there. Logo and images are uploaded separately.
/// </summary>
public sealed class SaveShowcaseRequest
{
    /// <summary>Required on create; ignored on update.</summary>
    public Guid? ProjectLineageId { get; init; }

    public string? Highlights { get; init; }
    public string DevelopmentStage { get; init; } = "Idea";
    public bool ShowTeamMembers { get; init; } = true;
    public IReadOnlyCollection<ShowcaseLinkRequest> Links { get; init; } = Array.Empty<ShowcaseLinkRequest>();

    /// <summary>Version the client last loaded; a stale value is rejected on update.</summary>
    public uint? RowVersion { get; init; }
}

public sealed class ShowcaseLinkDto
{
    public Guid Id { get; init; }
    public string LinkType { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public sealed class ShowcaseMediaDto
{
    public Guid Id { get; init; }
    public string MediaType { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? Caption { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// The Project Profile fields a showcase displays, read live from the project. Field names match the Project Profile
/// page (Project name, Description, Problem, Solution, Target users).
/// </summary>
public sealed class ShowcaseProfileDto
{
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Problem { get; init; }
    public string? Solution { get; init; }
    public string? TargetUsers { get; init; }
}

public sealed class ShowcaseDto
{
    public Guid Id { get; init; }
    public Guid ProjectLineageId { get; init; }
    public ShowcaseProfileDto Profile { get; init; } = new();
    public string? LogoUrl { get; init; }
    public string? Highlights { get; init; }
    public string DevelopmentStage { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool ShowTeamMembers { get; init; }
    public string? ReviewNote { get; init; }
    public DateTime? PublishedAt { get; init; }
    public bool IsFeatured { get; init; }
    public DateTime? FeaturedAt { get; init; }
    public int ViewCount { get; init; }
    public int LikeCount { get; init; }
    public int CommentCount { get; init; }
    public uint RowVersion { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool CanEdit { get; init; }
    public IReadOnlyCollection<ShowcaseLinkDto> Links { get; init; } = Array.Empty<ShowcaseLinkDto>();
    public IReadOnlyCollection<ShowcaseMediaDto> Media { get; init; } = Array.Empty<ShowcaseMediaDto>();
}

public sealed class SubmitShowcaseRequest
{
    /// <summary>The team confirmed it is allowed to publish the content (IP, unpublished research, personal data).</summary>
    public bool AcknowledgedPublishWarning { get; init; }
}

public sealed class ShowcaseDecisionRequest
{
    public string? Note { get; init; }
}

public sealed class ShowcaseModerationItemDto
{
    public Guid Id { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? LogoUrl { get; init; }
    public string Status { get; init; } = string.Empty;
    public string DevelopmentStage { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string? ReviewNote { get; init; }
    public DateTime? PublishedAt { get; init; }
    public bool IsFeatured { get; init; }
    public DateTime? FeaturedAt { get; init; }
    public DateTime SubmittedOrUpdatedAt { get; init; }
}

public sealed class ShowcaseModerationPageDto
{
    public IReadOnlyCollection<ShowcaseModerationItemDto> Items { get; init; } = Array.Empty<ShowcaseModerationItemDto>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class ShowcaseTeamMemberDto
{
    public string FullName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string Role { get; init; } = string.Empty;
}

public sealed class ShowcaseHistoryDto
{
    public string FromStatus { get; init; } = string.Empty;
    public string ToStatus { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class ShowcaseDetailDto
{
    public ShowcaseDto Showcase { get; init; } = new();
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyCollection<ShowcaseTeamMemberDto> Members { get; init; } = Array.Empty<ShowcaseTeamMemberDto>();

    /// <summary>Review trail; empty for ordinary viewers.</summary>
    public IReadOnlyCollection<ShowcaseHistoryDto> History { get; init; } = Array.Empty<ShowcaseHistoryDto>();
    public bool IsOwner { get; init; }
    public bool HasUpvoted { get; init; }
    public bool CanModerate { get; init; }
}

/// <summary>One project lineage the current student can manage, with its showcase when it exists.</summary>
public sealed class MyShowcaseEntryDto
{
    public Guid ProjectLineageId { get; init; }
    public Guid ProjectId { get; init; }

    /// <summary>Team that owns the project; the Project Profile page is addressed by team id.</summary>
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public ShowcaseProfileDto Profile { get; init; } = new();
    public ShowcaseDto? Showcase { get; init; }
}
