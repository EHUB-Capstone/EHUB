using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointEvaluations;

public interface ICheckpointEvaluationHandler
{
    Task<Result<WorkspaceCheckpointEvaluationSummaryResponse>> GetSummaryAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceCheckpointEvaluationResponse>> SaveAsync(
        Guid teamId, int checkpointNumber, SaveWorkspaceCheckpointEvaluationRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default);

    Task<Result<WorkspaceCheckpointEvaluationResponse>> UpdateAsync(
        Guid evaluationId, SaveWorkspaceCheckpointEvaluationRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default);
}
