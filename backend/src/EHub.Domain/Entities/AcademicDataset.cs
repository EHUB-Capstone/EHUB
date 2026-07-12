using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class AcademicDataset : AuditableEntity
{
    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public Guid? StudentId { get; set; }
    public virtual Student? Student { get; set; }

    public Guid? ProjectId { get; set; }
    public virtual Project? Project { get; set; }

    public AcademicDatasetType DatasetType { get; set; } = AcademicDatasetType.Custom;
    public string DynamicFieldsJson { get; set; } = "{}";

    public Guid? LastImportBatchId { get; set; }
    public virtual DataBankImportBatch? LastImportBatch { get; set; }

    // Navigation properties
    public virtual ICollection<DataBankFieldHistory> FieldHistories { get; set; } = new List<DataBankFieldHistory>();
}
