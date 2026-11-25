using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class ShowcaseReport : BaseEntity
{
    public Guid ShowcaseProjectId { get; set; }
    public Guid? CommentId { get; set; }
    public Guid? ReporterUserId { get; set; }
    public ShowcaseReportSource Source { get; set; } = ShowcaseReportSource.User;
    public string? ContentFingerprint { get; set; }
    public string? DetectionKey { get; set; }
    public ShowcaseReportReason Reason { get; set; }
    public ShowcaseViolationCategory? Category { get; set; }
    public string? Note { get; set; }
    // Captured at submission; never reconstructed from content that may have changed later.
    public string? ContentSnapshot { get; set; }
    public ShowcaseReportStatus Status { get; set; } = ShowcaseReportStatus.Open;
    public DateTime CreatedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public uint Version { get; set; }
}
