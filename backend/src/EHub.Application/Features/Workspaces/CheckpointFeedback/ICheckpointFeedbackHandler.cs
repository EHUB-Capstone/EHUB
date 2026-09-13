using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointFeedback;

public interface ICheckpointFeedbackHandler
{
    Task<Result<WorkspaceCheckpointFeedbackResponse>> CreateAsync(Guid teamId, int checkpointNumber, CreateWorkspaceCheckpointFeedbackRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid feedbackId, Guid userId, string role, CancellationToken cancellationToken = default);
}
