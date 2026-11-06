namespace EHub.Domain.Entities;

public sealed class ShowcaseUpvote
{
    public Guid ShowcaseProjectId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
