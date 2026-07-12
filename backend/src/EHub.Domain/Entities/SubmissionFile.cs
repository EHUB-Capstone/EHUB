using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class SubmissionFile : AuditableEntity
{
    public Guid SubmissionId { get; set; }
    public virtual Submission Submission { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string CloudinaryPublicId { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public SubmissionFileType FileType { get; set; } = SubmissionFileType.Report;

    public Guid? UploadedById { get; set; }
    public virtual User? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
