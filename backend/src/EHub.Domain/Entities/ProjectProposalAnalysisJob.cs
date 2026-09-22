using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectProposalAnalysisJob : BaseEntity
{
    public Guid ProposalVersionId { get; set; }
    public virtual ProjectProposalVersion ProposalVersion { get; set; } = null!;

    public ProjectProposalAnalysisJobStatus Status { get; set; } = ProjectProposalAnalysisJobStatus.Pending;
    public ProjectProposalAnalysisCandidateScope CandidateScope { get; set; } = ProjectProposalAnalysisCandidateScope.AllSystem;
    public bool IncludeCrossSemester { get; set; } = true;
    public ProjectProposalAnalysisLanguageMode LanguageMode { get; set; } = ProjectProposalAnalysisLanguageMode.VietnameseAndEnglish;
    public string ConfigurationVersion { get; set; } = "proposal-analysis-config-v1";

    public Guid RequestedByUserId { get; set; }
    public virtual User RequestedByUser { get; set; } = null!;

    public int AttemptCount { get; set; }
    public DateTime AvailableAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessingStartedAtUtc { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }

    public virtual ProjectProposalAnalysisResult? Result { get; set; }
}
