using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class WeeklyTaskTeamProgress : AuditableEntity
{
    public Guid WeeklyTaskId { get; set; }
    public WeeklyTask WeeklyTask { get; set; } = null!;

    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public WeeklyTaskStatus Status { get; set; } = WeeklyTaskStatus.Todo;
    public string? ChecklistJson { get; set; }
    public int CompletionPercentage { get; set; }
}
