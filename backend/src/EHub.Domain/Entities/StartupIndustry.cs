using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class StartupIndustry : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public StartupIndustryStatus Status { get; set; } = StartupIndustryStatus.Active;
}
