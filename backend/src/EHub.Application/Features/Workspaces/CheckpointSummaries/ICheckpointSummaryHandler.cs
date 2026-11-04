using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public interface ICheckpointSummaryHandler
{
    Task<Result<WorkspaceCheckpointSummaryResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues the chosen files, newest version first. Files with a current summary or already queued are skipped.
    /// Never waits for the model.
    /// </summary>
    Task<Result<WorkspaceCheckpointSummaryResponse>> RequestAsync(
        Guid teamId, int checkpointNumber, RequestCheckpointSummaryRequest request, Guid userId, string role,
        CancellationToken cancellationToken = default);

    /// <summary>Stops every queued or running file summary of the checkpoint. Finished summaries are kept.</summary>
    Task<Result<WorkspaceCheckpointSummaryResponse>> CancelAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default);
}
