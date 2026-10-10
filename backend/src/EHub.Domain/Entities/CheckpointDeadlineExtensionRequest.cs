using EHub.Domain.Common;

namespace EHub.Domain.Entities;

/// <summary>Immutable request from a team leader to reopen one missed checkpoint deadline.</summary>
public sealed class CheckpointDeadlineExtensionRequest : AuditableEntity
{
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public Guid CheckpointId { get; set; }
    public Checkpoint Checkpoint { get; set; } = null!;

    public Guid RequestedById { get; set; }
    public User RequestedBy { get; set; } = null!;

    public DateTime DeadlineUtc { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
}
