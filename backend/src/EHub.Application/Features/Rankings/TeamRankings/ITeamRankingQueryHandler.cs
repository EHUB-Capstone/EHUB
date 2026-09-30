using EHub.Contracts.Rankings;
using EHub.Shared.Results;

namespace EHub.Application.Features.Rankings.TeamRankings;

public interface ITeamRankingQueryHandler
{
    Task<Result<TeamRankingListResponse>> HandleAsync(
        GetTeamRankingsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
