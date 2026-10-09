using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class ProjectActivityLog : BaseEntity
{
    public Guid ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;
    public Guid? ActorUserId { get; set; }
    public virtual User? ActorUser { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? ChangedFieldsJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
