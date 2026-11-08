using EHub.Contracts.Teams;
using EHub.Shared.Results;

namespace EHub.Application.Features.Teams.FeedbackOverview;

public interface ITeamFeedbackOverviewHandler
{
    Task<Result<TeamFeedbackOverviewDto>> GetAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);
}
