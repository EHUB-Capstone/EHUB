using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

/// <summary>
/// The class-specific availability window for an Admin-defined checkpoint.
/// The checkpoint definition remains the single source of truth at course level.
/// </summary>
public sealed class ClassCheckpointSchedule : AuditableEntity
{
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public Guid CheckpointId { get; set; }
    public Checkpoint Checkpoint { get; set; } = null!;

    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public int ReopenCount { get; set; }
    public DateTime? LastReopenedAtUtc { get; set; }
}
