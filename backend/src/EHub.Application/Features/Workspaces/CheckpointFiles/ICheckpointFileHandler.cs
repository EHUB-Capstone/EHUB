using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

public interface ICheckpointFileHandler
{
    Task<Result<CheckpointFileDownload>> DownloadAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<CheckpointFileDownloadUrlResponse>> GetDownloadUrlAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<CheckpointFilePreviewSourceResponse>> GetPreviewSourceAsync(Guid teamId, int checkpointNumber, Guid fileId, bool retry, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<CheckpointFilePreview>> PreviewAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default);
}

public sealed record CheckpointFileDownload(byte[] Content, string ContentType, string OriginalName);
public sealed record CheckpointFilePreview(byte[] Content, string OriginalName, bool FromCache, PreviewTimings? Timings = null);

/// <summary>Where the time of one preview request went (milliseconds); exposed as a Server-Timing header.</summary>
public sealed record PreviewTimings(long AuthMs, long StorageMs, long ConvertMs);
