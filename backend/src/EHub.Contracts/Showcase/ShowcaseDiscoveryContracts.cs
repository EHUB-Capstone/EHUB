namespace EHub.Contracts.Showcase;

public sealed class GetShowcasesRequest
{
    public string? Search { get; init; }
    public string? Stage { get; init; }
    public string? Industry { get; init; }
    /// <summary>Omitted: all published pages; true: featured only; false: non-featured only.</summary>
    public bool? Featured { get; init; }
    /// <summary>Semester of the newest project in the lineage.</summary>
    public Guid? SemesterId { get; init; }
    public string Sort { get; init; } = "newest";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class ShowcaseCardDto
{
    public Guid Id { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? LogoUrl { get; init; }
    public string DevelopmentStage { get; init; } = string.Empty;
    public DateTime? PublishedAt { get; init; }
    public bool IsFeatured { get; init; }
    public DateTime? FeaturedAt { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Industries { get; init; } = Array.Empty<string>();
    public int LikeCount { get; init; }
    public int CommentCount { get; init; }
    /// <summary>Canonical YouTube or Vimeo watch URL of the page's first Video link; null when it has none that is supported.</summary>
    public string? VideoUrl { get; set; }
    /// <summary>The page's first uploaded image, shown as the preview of a feed post; null when it has none.</summary>
    public string? CoverUrl { get; init; }
}

public sealed class ShowcaseSemesterOptionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class ShowcaseFilterOptionsDto
{
    public IReadOnlyCollection<string> Industries { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<ShowcaseSemesterOptionDto> Semesters { get; init; } = Array.Empty<ShowcaseSemesterOptionDto>();
}
