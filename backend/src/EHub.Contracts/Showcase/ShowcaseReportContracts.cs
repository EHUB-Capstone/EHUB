namespace EHub.Contracts.Showcase;

public sealed class CreateShowcaseReportRequest
{
    public Guid? CommentId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? Note { get; init; }
}

public sealed class ResolveShowcaseReportRequest
{
    public string Status { get; init; } = string.Empty;
    public string? ResolutionNote { get; init; }
}

// The creation receipt never exposes the reporter or private moderation notes.
public sealed class ShowcaseReportReceiptDto
{
    public Guid Id { get; init; }
    public Guid ShowcaseProjectId { get; init; }
    public Guid? CommentId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

// Returned only by the staff report queue and resolve endpoints.
public sealed class ShowcaseReportDto
{
    public string? ContentSnapshot { get; init; }
    public IReadOnlyCollection<string> Sources { get; init; } = new[] { "User" };
    public string? SystemNote { get; init; }
    public int UserReportCount { get; init; }
    public int SystemReportCount { get; init; }
    public string? ShowcaseStatus { get; set; }
    public string? CommentContent { get; set; }
    public string? Category { get; init; }
    public Guid Id { get; init; }
    public Guid ShowcaseProjectId { get; init; }
    public Guid? CommentId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public Guid? ResolvedByUserId { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolutionNote { get; init; }
    /// <summary>Number of open reports for this same showcase or comment target.</summary>
    public int ReportCount { get; init; }
}
