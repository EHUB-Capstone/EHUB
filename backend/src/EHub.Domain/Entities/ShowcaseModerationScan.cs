using EHub.Domain.Common;

namespace EHub.Domain.Entities;

// Persists clean checks too, so deleting report history does not re-report unchanged content.
public sealed class ShowcaseModerationScan : BaseEntity
{
    public Guid ShowcaseProjectId { get; set; }
    public Guid? CommentId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public uint Version { get; set; }
}
