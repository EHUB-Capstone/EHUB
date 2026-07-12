using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class EvaluationHistory : BaseEntity
{
    public Guid EvaluationId { get; set; }
    public virtual Evaluation Evaluation { get; set; } = null!;

    public int Version { get; set; }
    public EvaluationHistoryAction Action { get; set; }

    public string SnapshotJson { get; set; } = string.Empty;

    public Guid ChangedById { get; set; }
    public virtual User ChangedBy { get; set; } = null!;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
