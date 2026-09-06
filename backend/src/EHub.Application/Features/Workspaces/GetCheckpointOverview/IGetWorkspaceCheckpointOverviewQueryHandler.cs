using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.GetCheckpointOverview;

public interface IGetWorkspaceCheckpointOverviewQueryHandler
{
    Task<Result<WorkspaceCheckpointOverviewResponse>> HandleAsync(
        Guid teamId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);
}
