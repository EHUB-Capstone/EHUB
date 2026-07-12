using System;
using System.Collections.Generic;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class ProjectComment : AuditableEntity
{
    public Guid ProjectProposalId { get; set; }
    public virtual ProjectProposal ProjectProposal { get; set; } = null!;

    public string SectionKey { get; set; } = string.Empty;
    public string? SectionLabel { get; set; }
    public string? SelectedText { get; set; }
    public string Content { get; set; } = string.Empty;

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }

    public Guid? ParentCommentId { get; set; }
    public virtual ProjectComment? ParentComment { get; set; }

    public Guid? ThreadRootId { get; set; }
    public virtual ProjectComment? ThreadRoot { get; set; }

    public bool Resolved { get; set; } = false;
    public Guid? ResolvedById { get; set; }
    public virtual User? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // Navigation properties for replies
    public virtual ICollection<ProjectComment> Replies { get; set; } = new List<ProjectComment>();
    public virtual ICollection<ProjectComment> ThreadReplies { get; set; } = new List<ProjectComment>();
}
