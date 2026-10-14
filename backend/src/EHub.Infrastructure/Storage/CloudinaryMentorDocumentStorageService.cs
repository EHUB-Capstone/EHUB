using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.Storage;

public sealed class CloudinaryMentorDocumentStorageService(Cloudinary cloudinary, IHttpClientFactory httpClientFactory,
    ILogger<CloudinaryMentorDocumentStorageService> logger)
    : IMentorDocumentStorageService
{
    public async Task<Result<(string Url, string PublicId)>> UploadAsync(Stream content, string fileName, Guid mentorId,
        string kind, CancellationToken cancellationToken)
    {
        var publicId = $"ehub/mentors/{mentorId:N}/{kind}/{Guid.NewGuid():N}";
        try
        {
            var uploaded = await cloudinary.UploadAsync(new RawUploadParams
            {
                File = new FileDescription(fileName, content), PublicId = publicId,
                Overwrite = false, UseFilename = false, UniqueFilename = false
            }, null, cancellationToken);
            return uploaded.Error is null && uploaded.SecureUrl is not null
                ? Result.Success((uploaded.SecureUrl.ToString(), uploaded.PublicId))
                : Result.Failure<(string, string)>(ErrorCodes.CommonUnexpectedError, "Document upload failed.");
        }
        catch
        {
            return Result.Failure<(string, string)>(ErrorCodes.CommonUnexpectedError, "Document upload failed.");
        }
    }

    public async Task<Result<byte[]>> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<byte[]>(ErrorCodes.CommonValidationError, "Stored document URL is invalid.");
        try
        {
            using var response = await httpClientFactory.CreateClient("CloudinarySubmissionFiles").GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return Result.Failure<byte[]>(ErrorCodes.CommonNotFoundError, "Document is unavailable.");
            if (response.Content.Headers.ContentLength > 10 * 1024 * 1024)
                return Result.Failure<byte[]>(ErrorCodes.CommonValidationError, "Stored document is too large.");
            await response.Content.LoadIntoBufferAsync(10 * 1024 * 1024, cancellationToken);
            return Result.Success(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        }
        catch
        {
            return Result.Failure<byte[]>(ErrorCodes.CommonUnexpectedError, "Document download failed.");
        }
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
        try { await cloudinary.DestroyAsync(new DeletionParams(publicId) { ResourceType = ResourceType.Raw }); }
        catch (Exception exception) { logger.LogWarning(exception, "Mentor document cleanup failed."); }
    }
}
