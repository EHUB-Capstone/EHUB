using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Storage;

public interface IMentorDocumentStorageService
{
    Task<Result<(string Url, string PublicId)>> UploadAsync(Stream content, string fileName, Guid mentorId,
        string kind, CancellationToken cancellationToken);
    Task<Result<byte[]>> DownloadAsync(string url, CancellationToken cancellationToken);
    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}
