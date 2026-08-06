using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Notification : AuditableEntity
{
    public Guid? SourceEventId { get; set; }
    public Guid RecipientUserId { get; set; }
    public virtual User RecipientUser { get; set; } = null!;

    public string? RecipientEmail { get; set; }
    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string DataJson { get; set; } = "{}";

    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }
}
