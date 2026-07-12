using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class WeeklyTask : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public WeeklyTaskType TaskType { get; set; } = WeeklyTaskType.Other;
    public WeeklyTaskScope Scope { get; set; } = WeeklyTaskScope.GlobalTemplate;
    public int WeekNumber { get; set; }

    public Guid? CourseId { get; set; }
    public virtual Course? Course { get; set; }

    public Guid? ClassId { get; set; }
    public virtual Class? Class { get; set; }

    public Guid? TeamId { get; set; }
    public virtual Team? Team { get; set; }

    public Guid? AssigneeStudentId { get; set; }
    public virtual Student? AssigneeStudent { get; set; }

    public WeeklyTaskStatus Status { get; set; } = WeeklyTaskStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }

    public string AttachmentsJson { get; set; } = "[]";
    public string ChecklistJson { get; set; } = "[]";
    public string[] Tags { get; set; } = Array.Empty<string>();

    public bool IsTemplate { get; set; } = false;
    public bool IsMandatory { get; set; } = false;
    public bool VisibleToStudents { get; set; } = true;

    public int CompletionPercentage { get; set; }
    public decimal? EstimatedHours { get; set; }

    public Guid CreatedById { get; set; }
    public virtual User Creator { get; set; } = null!;

    public Guid? UpdatedById { get; set; }
    public virtual User? Updater { get; set; }
}
