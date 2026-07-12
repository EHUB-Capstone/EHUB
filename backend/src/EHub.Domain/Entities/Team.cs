using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Team : AuditableEntity
{
    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public string TeamCode { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;

    public TeamStatus Status { get; set; } = TeamStatus.Active;

    public Guid? LeaderId { get; set; }
    public Guid? MentorId { get; set; }

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }

    public DateTime? ArchivedAt { get; set; }

    // Navigation properties
    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
    public virtual Project? Project { get; set; }
    public virtual ICollection<MentorAssignment> MentorAssignments { get; set; } = new List<MentorAssignment>();
    public virtual ICollection<ChatGroup> ChatGroups { get; set; } = new List<ChatGroup>();
    public virtual ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public virtual ICollection<SprintTask> SprintTasks { get; set; } = new List<SprintTask>();
    public virtual ICollection<WeeklyTask> WeeklyTasks { get; set; } = new List<WeeklyTask>();
}
