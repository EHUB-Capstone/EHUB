using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectProposalAnalysisResult : BaseEntity
{
    public Guid AnalysisJobId { get; set; }
    public virtual ProjectProposalAnalysisJob AnalysisJob { get; set; } = null!;

    public string Summary { get; set; } = string.Empty;
    public ProjectProposalOverlapRisk OverlapRisk { get; set; } = ProjectProposalOverlapRisk.InsufficientData;
    public string PotentialDifferentiatorsJson { get; set; } = "[]";
    public string LimitationsJson { get; set; } = "[]";
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string OutputSchemaVersion { get; set; } = string.Empty;
    public string EmbeddingProvider { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimension { get; set; }
    public string TextSchemaVersion { get; set; } = string.Empty;
    public string RetrievalVersion { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }

    public virtual ICollection<ProjectProposalAnalysisMatch> Matches { get; set; } = new List<ProjectProposalAnalysisMatch>();
}
