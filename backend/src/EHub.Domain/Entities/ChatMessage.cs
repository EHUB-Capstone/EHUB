using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ChatMessage : AuditableEntity
{
    public Guid ChatGroupId { get; set; }
    public virtual ChatGroup ChatGroup { get; set; } = null!;

    public Guid SenderUserId { get; set; }
    public virtual User SenderUser { get; set; } = null!;

    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;

    public string? Text { get; set; }
    public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;

    public string? AttachmentJson { get; set; }
    public string? ReactionsJson { get; set; }
    public string? MentionsJson { get; set; }

    public bool IsEdited { get; set; } = false;
    public DateTime? EditedAt { get; set; }

    public bool IsRevoked { get; set; } = false;
    public DateTime? RevokedAt { get; set; }
}
