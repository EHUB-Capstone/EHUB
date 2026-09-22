using EHub.Contracts.ProjectProposals;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProposalAnalyses;

public interface IProjectProposalAnalysisQueryHandler
{
    Task<Result<ProjectProposalAnalysisDto>> GetAsync(
        Guid jobId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);
}
