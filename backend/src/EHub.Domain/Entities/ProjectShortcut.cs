using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectShortcut : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ShortcutType ShortcutType { get; set; } = ShortcutType.Other;

    public Guid CreatedById { get; set; }
    public virtual User Creator { get; set; } = null!;
}
