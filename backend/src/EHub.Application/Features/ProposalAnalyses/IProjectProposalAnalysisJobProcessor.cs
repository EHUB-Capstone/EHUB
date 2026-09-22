namespace EHub.Application.Features.ProposalAnalyses;

public interface IProjectProposalAnalysisJobProcessor
{
    Task ProcessAsync(Guid jobId, string leaseOwner, CancellationToken cancellationToken = default);
}
