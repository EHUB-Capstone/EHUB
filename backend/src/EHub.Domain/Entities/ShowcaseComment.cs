using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public sealed class ShowcaseComment : AuditableEntity
{
    public Guid ShowcaseProjectId { get; set; }
    public ShowcaseProject ShowcaseProject { get; set; } = null!;
    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;
    public Guid? ParentCommentId { get; set; }
    public ShowcaseComment? ParentComment { get; set; }
    public ICollection<ShowcaseComment> Replies { get; set; } = new List<ShowcaseComment>();
    public string Content { get; set; } = string.Empty;
    /// <summary>Whether the author was an active member of the current owning team when posting.</summary>
    public bool IsTeamReply { get; set; }
}
