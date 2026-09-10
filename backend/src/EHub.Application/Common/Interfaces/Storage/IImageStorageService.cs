using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Storage;

public interface IImageStorageService
{
    Task<Result<ImageUploadResult>> UploadAvatarAsync(
        Stream content,
        string fileName,
        string contentType,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record ImageUploadResult(string SecureUrl);
