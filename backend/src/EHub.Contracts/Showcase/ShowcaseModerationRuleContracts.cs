namespace EHub.Contracts.Showcase;

public sealed class SaveShowcaseModerationRuleRequest
{
    public string Phrase { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Reason { get; init; } = "Harassment";
    public string? Status { get; init; }
    // Kept for callers deployed before the status contract. New screens send Status.
    public bool? IsEnabled { get; init; }
    public string? Category { get; init; }
    public uint? RowVersion { get; init; }
}

public sealed class ShowcaseModerationRuleDto
{
    public Guid Id { get; init; }
    public string Phrase { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsEnabled => Status == "Active";
    public string Category { get; init; } = string.Empty;
    public uint RowVersion { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public string? CreatedByName { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public Guid? UpdatedByUserId { get; init; }
    public string? UpdatedByName { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
