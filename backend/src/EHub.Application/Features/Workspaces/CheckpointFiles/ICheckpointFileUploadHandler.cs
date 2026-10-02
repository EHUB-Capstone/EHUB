using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

public interface ICheckpointFileUploadHandler
{
    Task<Result<CheckpointFileUploadSessionResponse>> InitiateAsync(Guid teamId, int checkpointNumber, InitiateCheckpointFileUploadRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceCheckpointFileResponse>> CompleteAsync(Guid teamId, int checkpointNumber, Guid uploadId, Guid userId, string role, CancellationToken cancellationToken = default);
}
