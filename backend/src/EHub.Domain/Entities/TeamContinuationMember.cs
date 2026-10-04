using System;

namespace EHub.Domain.Entities;

// Every student ever auto-added to a continued team, so a student the lecturer removed
// is not added back by a later import.
public class TeamContinuationMember
{
    public Guid ContinuationId { get; set; }
    public virtual TeamContinuation Continuation { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
