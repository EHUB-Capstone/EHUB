using System;
using System.Collections.Generic;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

// One row per (team lineage, target semester). It makes continuation idempotent
// across repeated or multi-batch imports and remembers when a continued team was dissolved.
public class TeamContinuation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeamLineageId { get; set; }

    // Null once the source (previous-semester) team has been permanently deleted.
    public Guid? SourceTeamId { get; set; }
    public virtual Team? SourceTeam { get; set; }

    public Guid TargetSemesterId { get; set; }
    public virtual Semester TargetSemester { get; set; } = null!;

    public Guid TargetClassId { get; set; }
    public virtual Class TargetClass { get; set; } = null!;

    public Guid? CreatedTeamId { get; set; }
    public virtual Team? CreatedTeam { get; set; }

    public TeamContinuationStatus Status { get; set; } = TeamContinuationStatus.Active;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DissolvedAtUtc { get; set; }
    public Guid? DissolvedByUserId { get; set; }

    public uint Version { get; set; }

    public virtual ICollection<TeamContinuationMember> Members { get; set; } = new List<TeamContinuationMember>();
}
