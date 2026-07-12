using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class DataBankColumn : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NormalizedKey { get; set; } = string.Empty;
    public DataBankColumnDataType DataType { get; set; } = DataBankColumnDataType.Text;

    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string[] NormalizedAliases { get; set; } = Array.Empty<string>();

    public bool IsSystemField { get; set; } = false;
    public bool IsActive { get; set; } = true;
}
