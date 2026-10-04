using System.Collections.Concurrent;
using System.Diagnostics;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

public interface ICheckpointPreviewGenerator
{
    /// <summary>
    /// Makes sure a PDF preview exists for a DOCX/PPTX submission file and returns it. The caller must
    /// already have authorised access to the file; this only checks the file belongs to <paramref name="teamId"/>.
    /// Never converts the same file version twice: concurrent callers wait for the first conversion.
    /// </summary>
    Task<Result<GeneratedPreview>> EnsurePreviewAsync(
        Guid fileId, Guid teamId, int checkpointNumber, bool needContent, CancellationToken cancellationToken = default);
}

/// <param name="Content">The PDF bytes, or null when the caller did not ask for them and a valid cache already existed.</param>
public sealed record GeneratedPreview(byte[]? Content, bool FromCache, string PreviewName, PreviewTimings Timings);

/// <summary>
/// Converts DOCX/PPTX files to a cached PDF preview. Used by the background job right after upload
/// and by the preview request as a fallback, so both share one lock per file and one cache.
/// </summary>
public sealed class CheckpointPreviewGenerator(
    IApplicationDbContext context,
    ISubmissionFileStorageService storage,
    ISubmissionObjectStorage objectStorage,
    IDateTimeProvider dateTimeProvider,
    IDocumentPreviewConverter previewConverter) : ICheckpointPreviewGenerator
{
    // In-process only: a second API instance could convert the same file once more (harmless, same output).
    private static readonly ConcurrentDictionary<Guid, PreviewLockEntry> PreviewLocks = new();

    public async Task<Result<GeneratedPreview>> EnsurePreviewAsync(
        Guid fileId, Guid teamId, int checkpointNumber, bool needContent, CancellationToken cancellationToken = default)
    {
        using (await AcquirePreviewLockAsync(fileId, cancellationToken))
        {
            // The lock may have been held by another conversion of this very file; always read fresh state.
            context.ClearChanges();
            var clock = Stopwatch.StartNew();
            long storageMs = 0, convertMs = 0;
            var file = await context.SubmissionFiles.Include(item => item.Submission)
                .FirstOrDefaultAsync(item => item.Id == fileId && item.Submission.TeamId == teamId, cancellationToken);
            if (file is null) return Result.Failure<GeneratedPreview>(ErrorCodes.CommonNotFoundError, "Submitted file was not found.");

            var extension = Path.GetExtension(file.OriginalName).ToLowerInvariant();
            if (extension is not ".docx" and not ".pptx")
            {
                return Result.Failure<GeneratedPreview>(
                    ErrorCodes.WorkspaceFilePreviewUnsupported, "This file format cannot be previewed. You can still download the original file.");
            }

            var isR2 = file.StorageProvider == SubmissionStorageProvider.R2;
            var hasCache = isR2
                ? !string.IsNullOrWhiteSpace(file.PreviewPdfPublicId)
                : !string.IsNullOrWhiteSpace(file.PreviewPdfUrl);
            if (hasCache && file.PreviewSourceVersionNumber == file.VersionNumber)
            {
                if (!needContent)
                {
                    await MarkReadyAsync(file, cancellationToken);
                    return Result.Success(new GeneratedPreview(null, FromCache: true, PreviewName(file), new PreviewTimings(0, 0, 0)));
                }

                var cached = isR2
                    ? await objectStorage.DownloadAsync(file.PreviewPdfPublicId!, cancellationToken)
                    : await DownloadLegacyAsync(file.PreviewPdfUrl!, cancellationToken);
                storageMs += clock.ElapsedMilliseconds;
                if (cached.IsSuccess && HasPdfSignature(cached.Value))
                {
                    await MarkReadyAsync(file, cancellationToken);
                    return Result.Success(new GeneratedPreview(cached.Value, FromCache: true, PreviewName(file), new PreviewTimings(0, storageMs, 0)));
                }
            }

            clock.Restart();
            var original = await ReadOriginalAsync(file, cancellationToken);
            storageMs += clock.ElapsedMilliseconds;
            if (original.IsFailure) return Result.Failure<GeneratedPreview>(original.Error);
            // R2 uploads are only magic-byte checked on complete; verify the Office structure before handing it to LibreOffice.
            if (isR2 && !CheckpointFileTypes.HasValidOfficeStructure(original.Value, extension))
            {
                return Result.Failure<GeneratedPreview>(
                    ErrorCodes.WorkspaceFilePreviewConversionFailed,
                    "The document could not be converted for preview. You can still download the original file.");
            }

            clock.Restart();
            var converted = await previewConverter.ConvertToPdfAsync(original.Value, extension, cancellationToken);
            convertMs += clock.ElapsedMilliseconds;
            if (converted.IsFailure) return Result.Failure<GeneratedPreview>(converted.Error);

            clock.Restart();
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
                    previewStream, PreviewName(file), "application/pdf", teamId, checkpointNumber, cancellationToken);
                if (uploaded.IsFailure) return PreviewCacheUnavailable();
                newPreviewId = uploaded.Value.PublicId;
                newPreviewUrl = uploaded.Value.SecureUrl;
            }

            file.PreviewPdfUrl = newPreviewUrl;
            file.PreviewPdfPublicId = newPreviewId;
            file.PreviewSourceVersionNumber = file.VersionNumber;
            file.PreviewGeneratedAt = EnsureUtc(dateTimeProvider.UtcNow);
            ApplyReady(file);
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

            storageMs += clock.ElapsedMilliseconds;
            return Result.Success(new GeneratedPreview(
                converted.Value.Content, FromCache: false, PreviewName(file), new PreviewTimings(0, storageMs, convertMs)));
        }
    }

    private async Task MarkReadyAsync(SubmissionFile file, CancellationToken cancellationToken)
    {
        if (file.PreviewStatus == SubmissionPreviewStatus.Ready) return;
        ApplyReady(file);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyReady(SubmissionFile file)
    {
        file.PreviewStatus = SubmissionPreviewStatus.Ready;
        file.PreviewAttemptCount = 0;
        file.PreviewNextAttemptAtUtc = null;
        file.PreviewLastError = null;
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

    internal static string PreviewName(SubmissionFile file) => $"{Path.GetFileNameWithoutExtension(file.OriginalName)}-preview-v{file.VersionNumber}.pdf";
    private static bool HasPdfSignature(byte[] content) => content.Length >= 5 && content.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
    private static Result<GeneratedPreview> PreviewCacheUnavailable() => Result.Failure<GeneratedPreview>(
        ErrorCodes.WorkspaceFilePreviewUnavailable,
        "The PDF preview could not be cached. You can still download the original file.");
    private static DateTime EnsureUtc(DateTime value) => CheckpointWorkspaceAccess.EnsureUtc(value);

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
}
