using EHub.Domain.Common;

namespace EHub.Domain.Entities;

/// <summary>
/// The newest submission version of a team's checkpoint that a staff member has opened. The review desk compares it with the
/// team's latest version to tell what is new since that person last looked. One row per person, team and checkpoint.
/// </summary>
public class CheckpointReviewMarker : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;
    public Guid CheckpointId { get; set; }
    public virtual Checkpoint Checkpoint { get; set; } = null!;
    public int SeenVersionNumber { get; set; }
    public DateTime SeenAtUtc { get; set; }
}
