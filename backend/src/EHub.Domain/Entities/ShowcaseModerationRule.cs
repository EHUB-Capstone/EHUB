using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class ShowcaseModerationRule : BaseEntity
{
    public string Phrase { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ShowcaseReportReason Reason { get; set; } = ShowcaseReportReason.Harassment;
    public ShowcaseViolationCategory Category { get; set; } = ShowcaseViolationCategory.Insult;
    public ShowcaseModerationRuleStatus Status { get; set; } = ShowcaseModerationRuleStatus.Active;
    // Null for legacy/default rules whose original author or creation time is unknown.
    public Guid? CreatedByUserId { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public uint Version { get; set; }
}
