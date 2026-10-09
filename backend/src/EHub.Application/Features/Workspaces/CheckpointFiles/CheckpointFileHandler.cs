using System.Diagnostics;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Access = EHub.Application.Features.Workspaces.CheckpointFiles.WorkspaceAccess;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

/// <summary>
/// Download, preview and delete for submitted files. Files uploaded to R2 are addressed by
/// <see cref="SubmissionFile.StorageKey"/>; older files still live on Cloudinary and keep their existing flow.
/// New uploads go through <see cref="CheckpointFileUploadHandler"/>; DOCX/PPTX conversion lives in
/// <see cref="CheckpointPreviewGenerator"/>.
/// </summary>
public sealed class CheckpointFileHandler(
    IApplicationDbContext context,
    ISubmissionFileStorageService storage,
    ISubmissionObjectStorage objectStorage,
    IDateTimeProvider dateTimeProvider,
    IDocumentPreviewConverter previewConverter) : ICheckpointFileHandler
{
    private readonly ICheckpointPreviewGenerator previewGenerator =
        new CheckpointPreviewGenerator(context, storage, objectStorage, dateTimeProvider, previewConverter);

    public async Task<Result<CheckpointFileDownload>> DownloadAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveFileAccessAsync(teamId, checkpointNumber, fileId, userId, role, tracking: false, cancellationToken);
        if (resolved.IsFailure) return Result.Failure<CheckpointFileDownload>(resolved.Error);
        var file = resolved.Value.File;
        var content = await ReadOriginalAsync(file, cancellationToken);
        return content.IsFailure
            ? Result.Failure<CheckpointFileDownload>(content.Error)
            : Result.Success(new CheckpointFileDownload(content.Value, file.MimeType, file.OriginalName));
    }

    public async Task<Result<CheckpointFileDownloadUrlResponse>> GetDownloadUrlAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveFileAccessAsync(teamId, checkpointNumber, fileId, userId, role, tracking: false, cancellationToken);
        if (resolved.IsFailure) return Result.Failure<CheckpointFileDownloadUrlResponse>(resolved.Error);
        var file = resolved.Value.File;
        if (file.StorageProvider != SubmissionStorageProvider.R2 || string.IsNullOrWhiteSpace(file.StorageKey))
        {
            return Invalid<CheckpointFileDownloadUrlResponse>("Direct download is not available for this file.");
        }

        var expiresAt = EnsureUtc(dateTimeProvider.UtcNow).Add(SubmissionFileLimits.PresignedDownloadLifetime);
        return Result.Success(new CheckpointFileDownloadUrlResponse
        {
            Url = objectStorage.CreatePresignedDownloadUrl(file.StorageKey, file.OriginalName, file.MimeType, SubmissionFileLimits.PresignedDownloadLifetime),
            ExpiresAt = expiresAt
        });
    }

    /// <summary>
    /// Tells the browser where to read the preview PDF without ever converting inside the request:
    /// a presigned R2 URL when it exists, otherwise "Preparing" after making sure the background job will build it.
    /// </summary>
    public async Task<Result<CheckpointFilePreviewSourceResponse>> GetPreviewSourceAsync(
        Guid teamId, int checkpointNumber, Guid fileId, bool retry, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveFileAccessAsync(teamId, checkpointNumber, fileId, userId, role, tracking: true, cancellationToken);
        if (resolved.IsFailure) return Result.Failure<CheckpointFilePreviewSourceResponse>(resolved.Error);
        var file = resolved.Value.File;

        // Files that still live on Cloudinary keep the existing server-side preview endpoint.
        if (file.StorageProvider != SubmissionStorageProvider.R2 || string.IsNullOrWhiteSpace(file.StorageKey))
        {
            return Source("Proxy");
        }

        var extension = Path.GetExtension(file.OriginalName).ToLowerInvariant();
        if (extension == ".pdf") return ReadySource(file.StorageKey);
        if (extension is not ".docx" and not ".pptx")
        {
            return Source("Unsupported", "This file format cannot be previewed. You can still download the original file.");
        }

        if (file.FileSize > SubmissionFileLimits.MaxPreviewConvertSizeBytes)
        {
            return Source("TooLarge",
                $"This file is larger than {SubmissionFileLimits.MaxPreviewConvertSizeBytes / (1024 * 1024)} MB and cannot be previewed. You can still download the original file.");
        }

        if (!string.IsNullOrWhiteSpace(file.PreviewPdfPublicId) && file.PreviewSourceVersionNumber == file.VersionNumber)
        {
            return ReadySource(file.PreviewPdfPublicId);
        }

        var now = EnsureUtc(dateTimeProvider.UtcNow);
        switch (file.PreviewStatus)
        {
            case SubmissionPreviewStatus.Pending:
                return Source("Preparing", retryAfterSeconds: 1);
            case SubmissionPreviewStatus.Failed when !retry:
                return Source("Failed", "The preview could not be prepared. You can still download the original file.");
            default:
                // None, a Ready row whose cache is gone, or an explicit retry of a Failed file: queue it again.
                file.PreviewStatus = SubmissionPreviewStatus.Pending;
                file.PreviewAttemptCount = 0;
                file.PreviewNextAttemptAtUtc = now;
                file.PreviewLastError = null;
                await context.SaveChangesAsync(cancellationToken);
                return Source("Preparing", retryAfterSeconds: 1);
        }
    }

    private Result<CheckpointFilePreviewSourceResponse> ReadySource(string objectKey)
    {
        var lifetime = SubmissionFileLimits.PresignedPreviewLifetime;
        return Result.Success(new CheckpointFilePreviewSourceResponse
        {
            Status = "Ready",
            Url = objectStorage.CreatePresignedInlinePdfUrl(objectKey, lifetime),
            ExpiresAt = EnsureUtc(dateTimeProvider.UtcNow).Add(lifetime)
        });
    }

    private static Result<CheckpointFilePreviewSourceResponse> Source(string status, string? message = null, int? retryAfterSeconds = null) =>
        Result.Success(new CheckpointFilePreviewSourceResponse { Status = status, Message = message, RetryAfterSeconds = retryAfterSeconds });

    public async Task<Result<CheckpointFilePreview>> PreviewAsync(
        Guid teamId,
        int checkpointNumber,
        Guid fileId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        var resolved = await ResolveFileAccessAsync(teamId, checkpointNumber, fileId, userId, role, tracking: false, cancellationToken);
        var authMs = clock.ElapsedMilliseconds;
        if (resolved.IsFailure) return Result.Failure<CheckpointFilePreview>(resolved.Error);

        var extension = Path.GetExtension(resolved.Value.File.OriginalName).ToLowerInvariant();
        if (extension == ".pdf")
        {
            clock.Restart();
            var pdf = await ReadOriginalAsync(resolved.Value.File, cancellationToken);
            var storageMs = clock.ElapsedMilliseconds;
            if (pdf.IsFailure) return Result.Failure<CheckpointFilePreview>(pdf.Error);
            if (!HasPdfSignature(pdf.Value)) return ConversionFailed<CheckpointFilePreview>();
            return Result.Success(new CheckpointFilePreview(
                pdf.Value, resolved.Value.File.OriginalName, FromCache: false, new PreviewTimings(authMs, storageMs, 0)));
        }

        if (extension is not ".docx" and not ".pptx")
        {
            return Result.Failure<CheckpointFilePreview>(
                ErrorCodes.WorkspaceFilePreviewUnsupported,
                "This file format cannot be previewed. You can still download the original file.");
        }

        if (resolved.Value.File.FileSize > SubmissionFileLimits.MaxPreviewConvertSizeBytes)
        {
            return Result.Failure<CheckpointFilePreview>(
                ErrorCodes.WorkspaceFilePreviewUnsupported,
                $"This file is larger than {SubmissionFileLimits.MaxPreviewConvertSizeBytes / (1024 * 1024)} MB and cannot be previewed. You can still download the original file.");
        }

        var generated = await previewGenerator.EnsurePreviewAsync(fileId, teamId, checkpointNumber, needContent: true, cancellationToken);
        if (generated.IsFailure) return Result.Failure<CheckpointFilePreview>(generated.Error);
        return Result.Success(new CheckpointFilePreview(
            generated.Value.Content!,
            generated.Value.PreviewName,
            generated.Value.FromCache,
            generated.Value.Timings with { AuthMs = authMs }));
    }

    public async Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsStudent(role)) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "Only team students can delete submitted files.");
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure(access.Error);
        if (!access.Value.IsMember) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var availability = EnsureOpen(access.Value.Schedule, now);
        if (availability.IsFailure) return availability;
        var file = await context.SubmissionFiles.Include(item => item.Submission)
            .FirstOrDefaultAsync(item => item.Id == fileId && item.Submission.TeamId == teamId && item.Submission.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
        if (file is null) return Result.Failure(ErrorCodes.CommonNotFoundError, "Submitted file was not found.");
        if (file.UploadedById != userId) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You can only delete files you uploaded.");
        file.IsDeleted = true; file.DeletedAt = now; file.DeletedBy = userId;
        await context.SaveChangesAsync(cancellationToken);

        var isR2 = file.StorageProvider == SubmissionStorageProvider.R2;
        await DeleteStoredAsync(isR2, isR2 ? file.StorageKey! : file.CloudinaryPublicId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(file.PreviewPdfPublicId))
        {
            await DeleteStoredAsync(isR2, file.PreviewPdfPublicId, cancellationToken);
        }
        return Result.Success();
    }

    private async Task<Result<byte[]>> ReadOriginalAsync(SubmissionFile file, CancellationToken cancellationToken)
    {
        if (file.StorageProvider != SubmissionStorageProvider.R2) return await DownloadLegacyAsync(file.FileUrl, cancellationToken);
        return string.IsNullOrWhiteSpace(file.StorageKey)
            ? Result.Failure<byte[]>(ErrorCodes.CommonNotFoundError, "Submitted file was not found.")
            : await objectStorage.DownloadAsync(file.StorageKey, cancellationToken);
    }

    private async Task<Result<byte[]>> DownloadLegacyAsync(string url, CancellationToken cancellationToken)
    {
        var download = await storage.DownloadAsync(url, cancellationToken);
        return download.IsFailure ? Result.Failure<byte[]>(download.Error) : Result.Success(download.Value.Content);
    }

    private Task DeleteStoredAsync(bool isR2, string id, CancellationToken cancellationToken) =>
        isR2 ? objectStorage.DeleteAsync(id, cancellationToken) : storage.DeleteAsync(id, cancellationToken);

    private async Task<Result<FileAccess>> ResolveFileAccessAsync(
        Guid teamId,
        int checkpointNumber,
        Guid fileId,
        Guid userId,
        string role,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<FileAccess>(access.Error);
        var query = context.SubmissionFiles.Include(item => item.Submission).AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        var file = await query.FirstOrDefaultAsync(item => item.Id == fileId &&
            item.Submission.TeamId == teamId && item.Submission.CheckpointId == access.Value.Checkpoint.Id,
            cancellationToken);
        return file is null
            ? Result.Failure<FileAccess>(ErrorCodes.CommonNotFoundError, "Submitted file was not found.")
            : Result.Success(new FileAccess(access.Value, file));
    }

    private Task<Result<Access>> ResolveAccessAsync(Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken) =>
        CheckpointWorkspaceAccess.ResolveAsync(context, teamId, checkpointNumber, userId, role, cancellationToken);

    private static bool HasPdfSignature(byte[] content) => content.Length >= 5 && content.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
    private static Result<T> ConversionFailed<T>() => Result.Failure<T>(
        ErrorCodes.WorkspaceFilePreviewConversionFailed,
        "The stored PDF is invalid and cannot be previewed. You can still download the original file.");
    private static bool IsStudent(string role) => CheckpointWorkspaceAccess.IsStudent(role);
    private static DateTime EnsureUtc(DateTime value) => CheckpointWorkspaceAccess.EnsureUtc(value);
    private static Result EnsureOpen(ClassCheckpointSchedule? schedule, DateTime now) => CheckpointWorkspaceAccess.EnsureOpen(schedule, now);
    private static Result<T> Invalid<T>(string message) => Result.Failure<T>(ErrorCodes.WorkspaceValidationError, message);

    private sealed record FileAccess(Access Workspace, SubmissionFile File);
}
