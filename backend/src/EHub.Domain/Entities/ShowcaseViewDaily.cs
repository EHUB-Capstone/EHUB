namespace EHub.Domain.Entities;

public sealed class ShowcaseViewDaily
{
    public Guid ShowcaseProjectId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly ViewDateUtc { get; set; }
}
