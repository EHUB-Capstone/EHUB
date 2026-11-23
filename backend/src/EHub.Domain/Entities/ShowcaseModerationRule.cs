using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class ShowcaseModerationRule : BaseEntity
{
    public string Phrase { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ShowcaseReportReason Reason { get; set; } = ShowcaseReportReason.Harassment;
    public ShowcaseViolationCategory Category { get; set; } = ShowcaseViolationCategory.Insult;
    public bool IsEnabled { get; set; } = true;
    public uint Version { get; set; }
}
