using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class DataBankSnapshot : BaseEntity
{
    public Guid ImportBatchId { get; set; }
    public virtual DataBankImportBatch ImportBatch { get; set; } = null!;

    public Guid CreatedById { get; set; }
    public virtual User CreatedBy { get; set; } = null!;

    public string ScopeJson { get; set; } = "{}";
    public string StudentSnapshotJson { get; set; } = "[]";
    public string DatasetSnapshotJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
