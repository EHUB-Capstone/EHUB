using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointLinks;

public interface ICheckpointLinkHandler
{
    Task<Result<WorkspaceCheckpointLinkResponse>> CreateAsync(Guid teamId, int checkpointNumber,
        SaveWorkspaceCheckpointLinkRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceCheckpointLinkResponse>> UpdateAsync(Guid teamId, int checkpointNumber, Guid linkId,
        SaveWorkspaceCheckpointLinkRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid linkId,
        Guid userId, string role, CancellationToken cancellationToken = default);
}
