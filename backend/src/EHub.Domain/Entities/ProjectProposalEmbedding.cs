using EHub.Domain.Common;

namespace EHub.Domain.Entities;

public class ProjectProposalEmbedding : BaseEntity
{
    public Guid ProposalVersionId { get; set; }
    public virtual ProjectProposalVersion ProposalVersion { get; set; } = null!;

    public string ContentHash { get; set; } = string.Empty;
    public string TextSchemaVersion { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Dimension { get; set; }
    public string VectorJson { get; set; } = "[]";
    public DateTime GeneratedAtUtc { get; set; }
}
