namespace EHub.Contracts.Showcase;

public sealed class SaveShowcaseModerationRuleRequest
{
    public string Phrase { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Reason { get; init; } = "Harassment";
    public bool IsEnabled { get; init; } = true;
    public string? Category { get; init; }
    public uint? RowVersion { get; init; }
}

public sealed class ShowcaseModerationRuleDto
{
    public Guid Id { get; init; }
    public string Phrase { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public string Category { get; init; } = string.Empty;
    public uint RowVersion { get; init; }
}
