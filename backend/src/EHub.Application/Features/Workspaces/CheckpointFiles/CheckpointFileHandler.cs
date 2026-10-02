using System.Collections.Concurrent;
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
/// New uploads go through <see cref="CheckpointFileUploadHandler"/>.
/// </summary>
public sealed class CheckpointFileHandler(
    IApplicationDbContext context,
    ISubmissionFileStorageService storage,
    ISubmissionObjectStorage objectStorage,
    IDateTimeProvider dateTimeProvider,
    IDocumentPreviewConverter previewConverter) : ICheckpointFileHandler
{
    private static readonly ConcurrentDictionary<Guid, PreviewLockEntry> PreviewLocks = new();

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

    public async Task<Result<CheckpointFilePreview>> PreviewAsync(
        Guid teamId,
        int checkpointNumber,
        Guid fileId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveFileAccessAsync(teamId, checkpointNumber, fileId, userId, role, tracking: false, cancellationToken);
        if (resolved.IsFailure) return Result.Failure<CheckpointFilePreview>(resolved.Error);

        var extension = Path.GetExtension(resolved.Value.File.OriginalName).ToLowerInvariant();
        if (extension == ".pdf")
        {
            var pdf = await ReadOriginalAsync(resolved.Value.File, cancellationToken);
            if (pdf.IsFailure) return Result.Failure<CheckpointFilePreview>(pdf.Error);
            if (!HasPdfSignature(pdf.Value)) return ConversionFailed<CheckpointFilePreview>();
            return Result.Success(new CheckpointFilePreview(pdf.Value, resolved.Value.File.OriginalName, FromCache: false));
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

        using (await AcquirePreviewLockAsync(fileId, cancellationToken))
        {
            context.ClearChanges();
            resolved = await ResolveFileAccessAsync(teamId, checkpointNumber, fileId, userId, role, tracking: true, cancellationToken);
            if (resolved.IsFailure) return Result.Failure<CheckpointFilePreview>(resolved.Error);
            var file = resolved.Value.File;
            var isR2 = file.StorageProvider == SubmissionStorageProvider.R2;

            var hasCache = isR2
                ? !string.IsNullOrWhiteSpace(file.PreviewPdfPublicId)
                : !string.IsNullOrWhiteSpace(file.PreviewPdfUrl);
            if (hasCache && file.PreviewSourceVersionNumber == file.VersionNumber)
            {
                var cached = isR2
                    ? await objectStorage.DownloadAsync(file.PreviewPdfPublicId!, cancellationToken)
                    : await DownloadLegacyAsync(file.PreviewPdfUrl!, cancellationToken);
                if (cached.IsSuccess && HasPdfSignature(cached.Value))
                {
                    return Result.Success(new CheckpointFilePreview(cached.Value, PreviewName(file), FromCache: true));
                }
            }

            var original = await ReadOriginalAsync(file, cancellationToken);
            if (original.IsFailure) return Result.Failure<CheckpointFilePreview>(original.Error);
            // R2 uploads are only magic-byte checked on complete; verify the Office structure before handing it to LibreOffice.
            if (isR2 && !CheckpointFileTypes.HasValidOfficeStructure(original.Value, extension))
            {
                return Result.Failure<CheckpointFilePreview>(
                    ErrorCodes.WorkspaceFilePreviewConversionFailed,
                    "The document could not be converted for preview. You can still download the original file.");
            }

            var converted = await previewConverter.ConvertToPdfAsync(original.Value, extension, cancellationToken);
            if (converted.IsFailure) return Result.Failure<CheckpointFilePreview>(converted.Error);

            var previousPreviewId = file.PreviewPdfPublicId;
            string newPreviewId;
            string? newPreviewUrl = null;
            if (isR2)
            {
                newPreviewId = $"{file.StorageKey}.preview.pdf";
                var stored = await objectStorage.UploadAsync(newPreviewId, converted.Value.Content, "application/pdf", cancellationToken);
                if (stored.IsFailure) return PreviewCacheUnavailable();
            }
            else
            {
                await using var previewStream = new MemoryStream(converted.Value.Content, writable: false);
                var uploaded = await storage.UploadAsync(
                    previewStream,
                    PreviewName(file),
                    "application/pdf",
                    teamId,
                    checkpointNumber,
                    cancellationToken);
                if (uploaded.IsFailure) return PreviewCacheUnavailable();
                newPreviewId = uploaded.Value.PublicId;
                newPreviewUrl = uploaded.Value.SecureUrl;
            }

            file.PreviewPdfUrl = newPreviewUrl;
            file.PreviewPdfPublicId = newPreviewId;
            file.PreviewSourceVersionNumber = file.VersionNumber;
            file.PreviewGeneratedAt = EnsureUtc(dateTimeProvider.UtcNow);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await DeleteStoredAsync(isR2, newPreviewId, cancellationToken);
                throw;
            }

            if (!string.IsNullOrWhiteSpace(previousPreviewId) &&
                !string.Equals(previousPreviewId, newPreviewId, StringComparison.Ordinal))
            {
                await DeleteStoredAsync(isR2, previousPreviewId, cancellationToken);
            }

            return Result.Success(new CheckpointFilePreview(converted.Value.Content, PreviewName(file), FromCache: false));
        }
    }

    private static async Task<PreviewLockLease> AcquirePreviewLockAsync(Guid fileId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var entry = PreviewLocks.GetOrAdd(fileId, static _ => new PreviewLockEntry());
            Interlocked.Increment(ref entry.ReferenceCount);
            if (PreviewLocks.TryGetValue(fileId, out var current) && ReferenceEquals(entry, current))
            {
                try
                {
                    await entry.Gate.WaitAsync(cancellationToken);
                    return new PreviewLockLease(fileId, entry);
                }
                catch
                {
                    ReleasePreviewLockReference(fileId, entry, releaseGate: false);
                    throw;
                }
            }

            ReleasePreviewLockReference(fileId, entry, releaseGate: false);
        }
    }

    private static void ReleasePreviewLockReference(Guid fileId, PreviewLockEntry entry, bool releaseGate)
    {
        if (releaseGate) entry.Gate.Release();
        if (Interlocked.Decrement(ref entry.ReferenceCount) == 0 &&
            PreviewLocks.TryRemove(new KeyValuePair<Guid, PreviewLockEntry>(fileId, entry)))
        {
            entry.Gate.Dispose();
        }
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

    private static string PreviewName(SubmissionFile file) => $"{Path.GetFileNameWithoutExtension(file.OriginalName)}-preview-v{file.VersionNumber}.pdf";
    private static bool HasPdfSignature(byte[] content) => content.Length >= 5 && content.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
    private static Result<T> ConversionFailed<T>() => Result.Failure<T>(
        ErrorCodes.WorkspaceFilePreviewConversionFailed,
        "The stored PDF is invalid and cannot be previewed. You can still download the original file.");
    private static Result<CheckpointFilePreview> PreviewCacheUnavailable() => Result.Failure<CheckpointFilePreview>(
        ErrorCodes.WorkspaceFilePreviewUnavailable,
        "The PDF preview could not be cached. You can still download the original file.");
    private static bool IsStudent(string role) => CheckpointWorkspaceAccess.IsStudent(role);
    private static DateTime EnsureUtc(DateTime value) => CheckpointWorkspaceAccess.EnsureUtc(value);
    private static Result EnsureOpen(ClassCheckpointSchedule? schedule, DateTime now) => CheckpointWorkspaceAccess.EnsureOpen(schedule, now);
    private static Result<T> Invalid<T>(string message) => Result.Failure<T>(ErrorCodes.WorkspaceValidationError, message);

    private sealed class PreviewLockEntry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int ReferenceCount;
    }

    private sealed class PreviewLockLease(Guid fileId, PreviewLockEntry entry) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                ReleasePreviewLockReference(fileId, entry, releaseGate: true);
            }
        }
    }

    private sealed record FileAccess(Access Workspace, SubmissionFile File);
}
