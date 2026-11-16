namespace EHub.Contracts.Showcase;

/// <summary>Feature or unfeature several showcases in one request.</summary>
public sealed class BulkFeatureShowcasesRequest
{
    public IReadOnlyList<Guid> Ids { get; init; } = [];
    public bool Featured { get; init; }
}

public sealed class BulkFeatureShowcasesResultDto
{
    /// <summary>Showcases whose featured flag changed.</summary>
    public int Updated { get; init; }
    /// <summary>Showcases already in the requested state.</summary>
    public int Unchanged { get; init; }
    /// <summary>Ids that are missing, unpublished or outside the caller's classes (indistinguishable on purpose).</summary>
    public int Skipped { get; init; }
}

/// <summary>A published showcase the caller may feature, with the class it belongs to.</summary>
public sealed class FeaturedCandidateDto
{
    public Guid Id { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string? LogoUrl { get; init; }
    public string DevelopmentStage { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public string ClassCode { get; init; } = string.Empty;
    public string LecturerName { get; init; } = string.Empty;
    public bool IsFeatured { get; init; }
}
