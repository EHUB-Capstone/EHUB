using EHub.Contracts.ProjectProposals;
using EHub.Shared.Results;

namespace EHub.Application.Features.ProjectProposals;

public interface IProjectProposalHandler
{
    Task<Result<ProjectProposalDto>> GetByTeamAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectProposalDto>> CreateAsync(Guid teamId, CreateProjectProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectProposalDto>> UpdateAsync(Guid proposalId, UpdateProjectProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectProposalDto>> SubmitAsync(Guid proposalId, SubmitProjectProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<ProjectProposalVersionSummaryDto>>> GetVersionsAsync(Guid proposalId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectProposalVersionDto>> GetVersionAsync(Guid proposalId, Guid versionId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectProposalDto>> RestoreVersionAsync(Guid proposalId, Guid versionId, RestoreProjectProposalVersionRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ProjectProposalDto>> ReviewAsync(Guid proposalId, ReviewProjectProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
}
