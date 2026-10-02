using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class TeamFormation : AuditableEntity
{
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public Guid CreatorStudentId { get; set; }
    public Student CreatorStudent { get; set; } = null!;
    public Guid ProposedLeaderStudentId { get; set; }
    public Student ProposedLeaderStudent { get; set; } = null!;
    public string TeamName { get; set; } = string.Empty;
    public string NormalizedTeamName { get; set; } = string.Empty;
    public TeamFormationStatus Status { get; set; } = TeamFormationStatus.Pending;
    public Guid? CompletedTeamId { get; set; }
    public Team? CompletedTeam { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public uint Version { get; set; }
    public ICollection<TeamFormationInvitation> Invitations { get; set; } = new List<TeamFormationInvitation>();
}
