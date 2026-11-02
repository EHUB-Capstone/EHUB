using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

/// <summary>Append-only record of every status change of a showcase.</summary>
public class ShowcaseReviewHistory : BaseEntity
{
    public Guid ShowcaseProjectId { get; set; }
    public virtual ShowcaseProject ShowcaseProject { get; set; } = null!;

    public ShowcaseStatus FromStatus { get; set; }
    public ShowcaseStatus ToStatus { get; set; }
    public string? Note { get; set; }

    public Guid ActorId { get; set; }
    public virtual User Actor { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
