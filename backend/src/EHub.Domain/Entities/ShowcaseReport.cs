using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public sealed class ShowcaseReport : BaseEntity
{
    public Guid ShowcaseProjectId { get; set; }
    public Guid? CommentId { get; set; }
    public Guid ReporterUserId { get; set; }
    public ShowcaseReportReason Reason { get; set; }
    public string? Note { get; set; }
    public ShowcaseReportStatus Status { get; set; } = ShowcaseReportStatus.Open;
    public DateTime CreatedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
    public uint Version { get; set; }
}
