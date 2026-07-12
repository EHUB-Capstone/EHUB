using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ChatGroup : AuditableEntity
{
    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public Guid? TeamId { get; set; }
    public virtual Team? Team { get; set; }

    public string GroupName { get; set; } = string.Empty;
    public ChatGroupType GroupType { get; set; } = ChatGroupType.CustomGroup;

    public Guid CreatedById { get; set; }
    public virtual User Creator { get; set; } = null!;

    // Navigation properties
    public virtual ICollection<ChatGroupMember> Members { get; set; } = new List<ChatGroupMember>();
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
