using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class DataBankFieldHistory : BaseEntity
{
    public Guid DatasetId { get; set; }
    public virtual AcademicDataset Dataset { get; set; } = null!;

    public string FieldKey { get; set; } = string.Empty;

    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }

    public Guid ImportBatchId { get; set; }
    public virtual DataBankImportBatch ImportBatch { get; set; } = null!;

    public Guid ImportedById { get; set; }
    public virtual User ImportedBy { get; set; } = null!;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public DataBankEntityType EntityType { get; set; } = DataBankEntityType.AcademicDataset;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
