using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointRequirements;

public interface ICheckpointRequirementHandler
{
    Task<Result<WorkspaceCheckpointSubmissionResponse>> UpdateAsync(
        Guid teamId,
        int checkpointNumber,
        UpdateWorkspaceCheckpointRequirementsRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);
}
