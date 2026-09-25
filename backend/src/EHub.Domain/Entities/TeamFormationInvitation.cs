using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class TeamFormationInvitation
{
    public Guid FormationId { get; set; }
    public TeamFormation Formation { get; set; } = null!;
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public ClassStudent ClassStudent { get; set; } = null!;
    public TeamInvitationStatus Status { get; set; } = TeamInvitationStatus.Pending;
    public DateTime? RespondedAtUtc { get; set; }
    public DateTime? ReservationReleasedAtUtc { get; set; }
}
