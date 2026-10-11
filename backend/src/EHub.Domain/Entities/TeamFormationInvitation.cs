using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

/// <summary>
/// One invitation record. A student can have several records in the same formation
/// (history); only the one with <see cref="ReservationReleasedAtUtc"/> == null is active.
/// </summary>
public sealed class TeamFormationInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormationId { get; set; }
    public TeamFormation Formation { get; set; } = null!;
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public ClassStudent ClassStudent { get; set; } = null!;
    public TeamInvitationStatus Status { get; set; } = TeamInvitationStatus.Pending;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public DateTime? ReservationReleasedAtUtc { get; set; }
}
