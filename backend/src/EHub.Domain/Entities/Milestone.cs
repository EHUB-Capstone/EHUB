using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Milestone : AuditableEntity
{
    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public virtual Project? Project { get; set; }

    public Guid? ClassId { get; set; }
    public virtual Class? Class { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime DueDate { get; set; }

    public MilestoneStatus Status { get; set; } = MilestoneStatus.NotStarted;
    public int Progress { get; set; }

    public Guid CreatedById { get; set; }
    public virtual User Creator { get; set; } = null!;

    public Guid? UpdatedById { get; set; }
    public virtual User? Updater { get; set; }

    // Navigation property
    public virtual ICollection<SprintTask> Tasks { get; set; } = new List<SprintTask>();
}
