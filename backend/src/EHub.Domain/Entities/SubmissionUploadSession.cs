using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

/// <summary>
/// Tracks one direct browser-to-R2 upload. No Submission/SubmissionFile exists until the
/// session is completed and the stored object has been verified.
/// </summary>
public class SubmissionUploadSession : BaseEntity
{
    public Guid TeamId { get; set; }
    public Guid CheckpointId { get; set; }
    public Guid UserId { get; set; }

    public string ObjectKey { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long DeclaredSize { get; set; }

    public SubmissionUploadSessionStatus Status { get; set; } = SubmissionUploadSessionStatus.Pending;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? SubmissionFileId { get; set; }

    public uint RowVersion { get; set; }
}
