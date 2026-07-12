using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class MentorProfile : AuditableEntity
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public string[] Expertise { get; set; } = Array.Empty<string>();
    public string? Bio { get; set; }
    public string? Organization { get; set; }
    public string? LinkedInUrl { get; set; }

    public MentorProfileStatus Status { get; set; } = MentorProfileStatus.Active;
    public int MaxTeams { get; set; } = 3;

    // Navigation properties
    public virtual ICollection<MentorAssignment> Assignments { get; set; } = new List<MentorAssignment>();
}
