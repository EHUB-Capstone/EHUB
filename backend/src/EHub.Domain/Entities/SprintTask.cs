using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class SprintTask : AuditableEntity
{
    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public Guid? ProjectId { get; set; }
    public virtual Project? Project { get; set; }

    public Guid? MilestoneId { get; set; }
    public virtual Milestone? Milestone { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? AssigneeUserId { get; set; }
    public virtual User? AssigneeUser { get; set; }

    public Guid? AssigneeStudentId { get; set; }
    public virtual Student? AssigneeStudent { get; set; }

    public SprintTaskStatus Status { get; set; } = SprintTaskStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int Position { get; set; }

    public Guid CreatedById { get; set; }
    public virtual User Creator { get; set; } = null!;

    public Guid? UpdatedById { get; set; }
    public virtual User? Updater { get; set; }
}
