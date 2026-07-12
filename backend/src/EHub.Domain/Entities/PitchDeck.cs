using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class PitchDeck : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    public Guid? ProjectProposalId { get; set; }
    public virtual ProjectProposal? ProjectProposal { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string CloudinaryPublicId { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }

    public int VersionNumber { get; set; }

    public Guid UploadedById { get; set; }
    public virtual User UploadedBy { get; set; } = null!;

    public PitchDeckStatus Status { get; set; } = PitchDeckStatus.Draft;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
