using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ChatGroupMember : AuditableEntity
{
    public Guid ChatGroupId { get; set; }
    public virtual ChatGroup ChatGroup { get; set; } = null!;

    public Guid? UserId { get; set; }
    public virtual User? User { get; set; }

    public Guid? StudentId { get; set; }
    public virtual Student? Student { get; set; }

    public ChatMemberRole Role { get; set; } = ChatMemberRole.Member;
    public string? Nickname { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public bool IsActive { get; set; } = true;
}
