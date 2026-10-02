using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Storage;

/// <summary>
/// Private S3-compatible object storage (Cloudflare R2) for Submission Documents.
/// The browser uploads straight to the bucket through a presigned PUT; the API never receives the bytes.
/// </summary>
public interface ISubmissionObjectStorage
{
    PresignedObjectUpload CreatePresignedUpload(
        string objectKey,
        string contentType,
        long contentLength,
        TimeSpan lifetime);

    /// <summary>Returns <c>CommonNotFoundError</c> when the object does not exist.</summary>
    Task<Result<StoredObjectInfo>> GetObjectInfoAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<Result<byte[]>> ReadRangeAsync(string objectKey, long offset, int length, CancellationToken cancellationToken = default);

    Task<Result<byte[]>> DownloadAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<Result> UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Short-lived GET URL that makes the browser save the object as <paramref name="fileName"/>.</summary>
    string CreatePresignedDownloadUrl(string objectKey, string fileName, string contentType, TimeSpan lifetime);

    /// <summary>Best effort; deleting a missing object is not an error.</summary>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}

public sealed record PresignedObjectUpload(string Url, IReadOnlyDictionary<string, string> Headers);

public sealed record StoredObjectInfo(long Size, string? ContentType);
