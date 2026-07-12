using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class StartupLineage : AuditableEntity
{
    public string StartupName { get; set; } = string.Empty;

    public Guid OriginalProjectId { get; set; }
    public virtual Project OriginalProject { get; set; } = null!;

    public Guid CurrentProjectId { get; set; }
    public virtual Project CurrentProject { get; set; } = null!;

    public StartupLineageStatus Status { get; set; } = StartupLineageStatus.Active;

    public Guid CreatedById { get; set; }
    public virtual User Creator { get; set; } = null!;
}
