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
    public int VersionNumber { get; set; } = 1;
    public string FileUrl { get; set; } = string.Empty;
    public string CloudinaryPublicId { get; set; } = string.Empty;
    // R2 files keep FileUrl/CloudinaryPublicId empty and are addressed by StorageKey only.
    public SubmissionStorageProvider StorageProvider { get; set; } = SubmissionStorageProvider.Cloudinary;
    public string? StorageKey { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public SubmissionFileType FileType { get; set; } = SubmissionFileType.Report;

    public string? PreviewPdfUrl { get; set; }
    public string? PreviewPdfPublicId { get; set; }
    public int? PreviewSourceVersionNumber { get; set; }
    public DateTime? PreviewGeneratedAt { get; set; }

    public Guid? UploadedById { get; set; }
    public virtual User? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
