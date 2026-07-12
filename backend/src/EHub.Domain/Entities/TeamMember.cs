using System;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class TeamMember
{
    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public virtual ClassStudent ClassStudent { get; set; } = null!;

    public TeamMemberRole RoleInTeam { get; set; } = TeamMemberRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedById { get; set; }
    public virtual User? CreatedBy { get; set; }
}
