using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

public interface ICheckpointFileHandler
{
    Task<Result<WorkspaceCheckpointFileResponse>> UploadAsync(Guid teamId, int checkpointNumber, Stream content, string originalName, string contentType, long length, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<CheckpointFileDownload>> DownloadAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default);
}

public sealed record CheckpointFileDownload(byte[] Content, string ContentType, string OriginalName);
