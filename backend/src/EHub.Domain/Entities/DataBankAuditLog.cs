using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class DataBankAuditLog : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public DataBankAuditAction Action { get; set; }
    public string Entity { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }

    public string DetailsJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
