using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public interface ICheckpointSummaryHandler
{
    Task<Result<WorkspaceCheckpointSummaryResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>Queues a summary of the team's current files and links. Never waits for the model.</summary>
    Task<Result<WorkspaceCheckpointSummaryResponse>> RequestAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default);
}
