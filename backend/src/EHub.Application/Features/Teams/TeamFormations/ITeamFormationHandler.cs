using EHub.Contracts.Teams;
using EHub.Shared.Results;

namespace EHub.Application.Features.Teams.TeamFormations;

public interface ITeamFormationHandler
{
    Task<Result<TeamFormationDto>> CreateAsync(Guid classId, CreateTeamFormationRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<TeamFormationDto>>> GetMineAsync(Guid? classId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<TeamFormationDto>>> GetPendingInvitationsAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamFormationDto>> GetAsync(Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamFormationDto>> AcceptAsync(Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamFormationDto>> DeclineAsync(Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamFormationDto>> CancelAsync(Guid formationId, Guid userId, string role, CancellationToken cancellationToken = default);
}
