using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Storage;

public interface ISubmissionFileStorageService
{
    Task<Result<SubmissionFileUploadResult>> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        Guid teamId,
        int checkpointNumber,
        CancellationToken cancellationToken = default);

    Task<Result<SubmissionFileDownloadResult>> DownloadAsync(
        string secureUrl,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string publicId, CancellationToken cancellationToken = default);
}

public sealed record SubmissionFileUploadResult(string SecureUrl, string PublicId);
public sealed record SubmissionFileDownloadResult(byte[] Content, string ContentType);
