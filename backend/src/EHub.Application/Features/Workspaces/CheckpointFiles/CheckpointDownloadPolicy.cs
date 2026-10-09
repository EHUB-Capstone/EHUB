using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

internal static class CheckpointDownloadPolicy
{
    /// <summary>Large R2 files are served by a presigned GET; Cloudinary files and small R2 files go through the API.</summary>
    public static bool CanDirectDownload(SubmissionFile file) =>
        file.StorageProvider == SubmissionStorageProvider.R2 &&
        !string.IsNullOrWhiteSpace(file.StorageKey) &&
        file.FileSize > SubmissionFileLimits.DirectDownloadThresholdBytes;
}
