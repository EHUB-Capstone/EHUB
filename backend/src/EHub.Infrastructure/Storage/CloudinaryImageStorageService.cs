using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.Extensions.Logging;
using StoredImageUploadResult = EHub.Application.Common.Interfaces.Storage.ImageUploadResult;

namespace EHub.Infrastructure.Storage;

public sealed class CloudinaryImageStorageService : IImageStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageStorageService> _logger;

    public CloudinaryImageStorageService(
        Cloudinary cloudinary,
        ILogger<CloudinaryImageStorageService> logger)
    {
        _cloudinary = cloudinary;
        _logger = logger;
    }

    public async Task<Result<StoredImageUploadResult>> UploadAvatarAsync(
        Stream content,
        string fileName,
        string contentType,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var upload = await _cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(fileName, content),
                Folder = "ehub/avatars",
                PublicId = userId.ToString("N"),
                Overwrite = true,
                Invalidate = true
            }, cancellationToken);

            if (upload.Error is not null || upload.SecureUrl is null)
            {
                _logger.LogWarning(
                    "Cloudinary avatar upload failed. StatusCode: {StatusCode}; Error: {Error}",
                    upload.StatusCode,
                    upload.Error?.Message ?? "Cloudinary did not return a secure URL.");

                return Result.Failure<StoredImageUploadResult>(
                    ErrorCodes.AuthProfileImageUploadFailed,
                    "Avatar upload could not be completed.");
            }

            return Result.Success(new StoredImageUploadResult(upload.SecureUrl.ToString()));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Cloudinary avatar upload threw an exception for authenticated profile update.");

            return Result.Failure<StoredImageUploadResult>(
                ErrorCodes.AuthProfileImageUploadFailed,
                "Avatar upload could not be completed.");
        }
    }
}
