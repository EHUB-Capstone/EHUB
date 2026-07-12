using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class DataBankImportBatch : AuditableEntity
{
    public string FileName { get; set; } = string.Empty;
    public string FileChecksum { get; set; } = string.Empty;

    public Guid UploadedById { get; set; }
    public virtual User UploadedBy { get; set; } = null!;

    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public string SheetName { get; set; } = string.Empty;
    public int HeaderRow { get; set; } = 1;

    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public int RowsSkipped { get; set; }

    public string[] ColumnsAdded { get; set; } = Array.Empty<string>();
    public string[] ColumnsIgnored { get; set; } = Array.Empty<string>();

    public string? ConflictsJson { get; set; }
    public string? AnalysisJson { get; set; }
    public string? ColumnMappingsJson { get; set; }

    public DataBankImportBatchStatus Status { get; set; } = DataBankImportBatchStatus.Previewed;

    public DateTime? CommittedAt { get; set; }
    public DateTime? RolledBackAt { get; set; }

    // Navigation properties
    public virtual DataBankSnapshot? Snapshot { get; set; }
    public virtual ICollection<DataBankFieldHistory> FieldHistories { get; set; } = new List<DataBankFieldHistory>();
    public virtual ICollection<AcademicDataset> AcademicDatasets { get; set; } = new List<AcademicDataset>();
}
