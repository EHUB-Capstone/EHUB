using EHub.Contracts.Teams;
using EHub.Shared.Results;

namespace EHub.Application.Features.Teams.TeamProposals;

public interface ITeamProposalHandler
{
    Task<Result<IReadOnlyCollection<TeamProposalDto>>> GetForClassAsync(Guid classId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamProposalDto>> CreateAsync(Guid classId, CreateTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamProposalDto>> UpdateAsync(Guid proposalId, UpdateTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamProposalDto>> SubmitAsync(Guid proposalId, SubmitTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamProposalDto>> CancelAsync(Guid proposalId, CancelTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamProposalDto>> ReviewAsync(Guid proposalId, ReviewTeamProposalRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<TeamProposalHistoryDto>>> GetHistoryAsync(Guid proposalId, Guid userId, string role, CancellationToken cancellationToken = default);
}
