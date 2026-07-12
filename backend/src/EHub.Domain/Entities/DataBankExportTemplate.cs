using System;
using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class DataBankExportTemplate : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }
    public virtual User Owner { get; set; } = null!;

    public string[] SelectedColumns { get; set; } = Array.Empty<string>();
    public string[] ColumnOrder { get; set; } = Array.Empty<string>();

    public string HeaderAliasesJson { get; set; } = "{}";
    public string FiltersJson { get; set; } = "{}";
}
