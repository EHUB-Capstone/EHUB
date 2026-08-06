using EHub.Contracts.Teams;
using EHub.Shared.Results;

namespace EHub.Application.Features.Teams.ManageTeams;

public interface ITeamManagementHandler
{
    Task<Result<IReadOnlyCollection<TeamDto>>> GetAccessibleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<TeamDto>>> GetForClassAsync(Guid classId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> GetAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> CreateAsync(Guid classId, CreateTeamRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> UpdateMembersAsync(Guid teamId, UpdateTeamMembersRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> AssignLeaderAsync(Guid teamId, AssignTeamLeaderRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
}
