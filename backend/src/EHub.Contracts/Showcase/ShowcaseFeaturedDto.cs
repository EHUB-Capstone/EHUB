namespace EHub.Contracts.Showcase;

public sealed class ShowcaseFeaturedDto
{
    public Guid Id { get; init; }
    public bool IsFeatured { get; init; }
    public DateTime? FeaturedAt { get; init; }
    public uint RowVersion { get; init; }
}
