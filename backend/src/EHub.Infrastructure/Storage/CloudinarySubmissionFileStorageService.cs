using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.Storage;

public sealed class CloudinarySubmissionFileStorageService(
    Cloudinary cloudinary,
    IHttpClientFactory httpClientFactory,
    ILogger<CloudinarySubmissionFileStorageService> logger) : ISubmissionFileStorageService
{
    public async Task<Result<SubmissionFileUploadResult>> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        Guid teamId,
        int checkpointNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var publicId = $"ehub/submissions/{teamId:N}/checkpoint-{checkpointNumber}/{Guid.NewGuid():N}";
            var upload = await cloudinary.UploadAsync(new RawUploadParams
            {
                File = new FileDescription(fileName, content),
                PublicId = publicId,
                Overwrite = false,
                UseFilename = false,
                UniqueFilename = false
            }, null, cancellationToken);

            if (upload.Error is not null || upload.SecureUrl is null)
            {
                logger.LogWarning("Cloudinary submission upload failed. StatusCode: {StatusCode}; Error: {Error}",
                    upload.StatusCode, upload.Error?.Message ?? "Cloudinary did not return a secure URL.");
                return Result.Failure<SubmissionFileUploadResult>(ErrorCodes.WorkspaceValidationError, "File upload could not be completed.");
            }

            return Result.Success(new SubmissionFileUploadResult(upload.SecureUrl.ToString(), upload.PublicId));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Cloudinary submission upload failed for team {TeamId}, checkpoint {CheckpointNumber}.", teamId, checkpointNumber);
            return Result.Failure<SubmissionFileUploadResult>(ErrorCodes.WorkspaceValidationError, "File upload could not be completed.");
        }
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return;
        try
        {
            await cloudinary.DestroyAsync(new DeletionParams(publicId) { ResourceType = ResourceType.Raw });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Cloudinary submission deletion failed for public id {PublicId}.", publicId);
        }
    }

    public async Task<Result<SubmissionFileDownloadResult>> DownloadAsync(string secureUrl, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(secureUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.EndsWith("cloudinary.com", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<SubmissionFileDownloadResult>(ErrorCodes.WorkspaceValidationError, "The stored file URL is invalid.");

        try
        {
            using var response = await httpClientFactory.CreateClient("CloudinarySubmissionFiles").GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Cloudinary submission download failed with status {StatusCode}.", response.StatusCode);
                return Result.Failure<SubmissionFileDownloadResult>(ErrorCodes.CommonNotFoundError, "The requested file is no longer available.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return Result.Success(new SubmissionFileDownloadResult(bytes, contentType));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Cloudinary submission download failed.");
            return Result.Failure<SubmissionFileDownloadResult>(ErrorCodes.CommonUnexpectedError, "The file could not be downloaded.");
        }
    }
}
