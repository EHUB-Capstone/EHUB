namespace EHub.Contracts.ProjectProposals;

public abstract class ProjectProposalContentRequest
{
    public string Title { get; init; } = string.Empty;
    public string StartupName { get; init; } = string.Empty;
    public string Tagline { get; init; } = string.Empty;
    public string Problem { get; init; } = string.Empty;
    public string Solution { get; init; } = string.Empty;
    public string TargetCustomers { get; init; } = string.Empty;
    public string ValueProposition { get; init; } = string.Empty;
    public string MarketSize { get; init; } = string.Empty;
    public string Competitors { get; init; } = string.Empty;
    public string BusinessModel { get; init; } = string.Empty;
    public string RevenueModel { get; init; } = string.Empty;
    public string MarketingStrategy { get; init; } = string.Empty;
    public string Technology { get; init; } = string.Empty;
    public string FinancialPlan { get; init; } = string.Empty;
    public string Roadmap { get; init; } = string.Empty;
    public string TeamIntroduction { get; init; } = string.Empty;
    public string ChangeNote { get; init; } = string.Empty;
}

public sealed class CreateProjectProposalRequest : ProjectProposalContentRequest;

public sealed class UpdateProjectProposalRequest : ProjectProposalContentRequest
{
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class SubmitProjectProposalRequest
{
    public string RowVersion { get; init; } = string.Empty;
    public string ChangeNote { get; init; } = string.Empty;
}

public sealed class RestoreProjectProposalVersionRequest
{
    public string RowVersion { get; init; } = string.Empty;
    public string ChangeNote { get; init; } = string.Empty;
}

public sealed class ReviewProjectProposalRequest
{
    public string Decision { get; init; } = string.Empty;
    public string Feedback { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProjectProposalDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid TeamId { get; init; }
    public Guid ClassId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StartupName { get; init; } = string.Empty;
    public string Tagline { get; init; } = string.Empty;
    public string Problem { get; init; } = string.Empty;
    public string Solution { get; init; } = string.Empty;
    public string TargetCustomers { get; init; } = string.Empty;
    public string ValueProposition { get; init; } = string.Empty;
    public string MarketSize { get; init; } = string.Empty;
    public string Competitors { get; init; } = string.Empty;
    public string BusinessModel { get; init; } = string.Empty;
    public string RevenueModel { get; init; } = string.Empty;
    public string MarketingStrategy { get; init; } = string.Empty;
    public string Technology { get; init; } = string.Empty;
    public string FinancialPlan { get; init; } = string.Empty;
    public string Roadmap { get; init; } = string.Empty;
    public string TeamIntroduction { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? CurrentSubmittedVersionId { get; init; }
    public Guid? CurrentAnalysisJobId { get; init; }
    public string? CurrentAnalysisStatus { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public DateTime? RejectedAtUtc { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyCollection<ProjectProposalReviewDto> Reviews { get; init; } = Array.Empty<ProjectProposalReviewDto>();
}

public sealed class ProjectProposalSnapshotDto
{
    public string Title { get; init; } = string.Empty;
    public string StartupName { get; init; } = string.Empty;
    public string Tagline { get; init; } = string.Empty;
    public string Problem { get; init; } = string.Empty;
    public string Solution { get; init; } = string.Empty;
    public string TargetCustomers { get; init; } = string.Empty;
    public string ValueProposition { get; init; } = string.Empty;
    public string MarketSize { get; init; } = string.Empty;
    public string Competitors { get; init; } = string.Empty;
    public string BusinessModel { get; init; } = string.Empty;
    public string RevenueModel { get; init; } = string.Empty;
    public string MarketingStrategy { get; init; } = string.Empty;
    public string Technology { get; init; } = string.Empty;
    public string FinancialPlan { get; init; } = string.Empty;
    public string Roadmap { get; init; } = string.Empty;
    public string TeamIntroduction { get; init; } = string.Empty;
}

public sealed class ProjectProposalVersionSummaryDto
{
    public Guid Id { get; init; }
    public int VersionNumber { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string SnapshotSchemaVersion { get; init; } = string.Empty;
    public string ChangeNote { get; init; } = string.Empty;
    public Guid ChangedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class ProjectProposalVersionDto
{
    public Guid Id { get; init; }
    public Guid ProjectProposalId { get; init; }
    public int VersionNumber { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string SnapshotSchemaVersion { get; init; } = string.Empty;
    public string ChangeNote { get; init; } = string.Empty;
    public Guid ChangedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public ProjectProposalSnapshotDto Snapshot { get; init; } = new();
}

public sealed class ProjectProposalReviewDto
{
    public Guid Id { get; init; }
    public Guid ProposalVersionId { get; init; }
    public string FromStatus { get; init; } = string.Empty;
    public string ToStatus { get; init; } = string.Empty;
    public string Feedback { get; init; } = string.Empty;
    public Guid ReviewedByUserId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}
