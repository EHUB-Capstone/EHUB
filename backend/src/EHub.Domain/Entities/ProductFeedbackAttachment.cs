using EHub.Domain.Common;
namespace EHub.Domain.Entities;
public sealed class ProductFeedbackAttachment : AuditableEntity
{ public Guid ProductFeedbackId { get; set; } public ProductFeedback ProductFeedback { get; set; } = null!; public string OriginalName { get; set; } = string.Empty; public string MimeType { get; set; } = string.Empty; public long FileSize { get; set; } public string FileUrl { get; set; } = string.Empty; public string CloudinaryPublicId { get; set; } = string.Empty; }
