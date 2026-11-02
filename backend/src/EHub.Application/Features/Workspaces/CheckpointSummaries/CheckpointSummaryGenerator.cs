using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public interface ICheckpointSummaryGenerator
{
    /// <returns>True when a file was handled (or lost to another worker), so the caller should poll again immediately.</returns>
    Task<bool> ProcessNextAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Summarises one queued file: reads its text up to the input limit, condenses it without a model to the prompt budget,
/// makes one model call and validates the answer. Requests are taken oldest first and, within a request, newest file
/// version first. Rows are claimed with a lease so overlapping runs never process the same file twice; transient
/// failures are retried with a back-off; a reviewer can cancel, which aborts the model call.
/// </summary>
public sealed class CheckpointSummaryGenerator(
    IApplicationDbContext context,
    ISubmissionObjectStorage objectStorage,
    ISubmissionFileStorageService legacyStorage,
    ISubmissionTextExtractor extractor,
    ICheckpointSummarizer summarizer,
    IDateTimeProvider dateTimeProvider,
    CheckpointSummarySettings settings,
    ICheckpointSummaryCancellation cancellations,
    ILogger<CheckpointSummaryGenerator> logger) : ICheckpointSummaryGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record Generated(string ContentJson, bool InputTruncated);

    public async Task<bool> ProcessNextAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var candidate = await context.SubmissionFileSummaries.AsNoTracking()
            .Where(item => (item.Status == CheckpointSummaryStatus.Pending && (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= nowUtc)) ||
                (item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc <= nowUtc))
            .OrderBy(item => item.NextAttemptAtUtc).ThenByDescending(item => item.FileVersionNumber)
            .Select(item => new { item.Id, item.SubmissionFileId, item.CheckpointId, item.AttemptCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return false;

        // The lease doubles as the ownership token checked when saving. PostgreSQL keeps microseconds, so round to them
        // or the saved value would never equal the one held here.
        var lease = (nowUtc + CheckpointSummaryLimits.ProcessingLease).Ticks;
        var leaseUntil = new DateTime(lease - lease % 10, DateTimeKind.Utc);
        var claimed = await context.SubmissionFileSummaries
            .Where(item => item.Id == candidate.Id &&
                ((item.Status == CheckpointSummaryStatus.Pending && (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= nowUtc)) ||
                 (item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc <= nowUtc)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, CheckpointSummaryStatus.Processing)
                .SetProperty(item => item.NextAttemptAtUtc, leaseUntil), cancellationToken);
        if (claimed == 0) return true;

        using var registration = cancellations.Register(candidate.Id, out var cancelledByReviewer);
        // A reviewer may have cancelled between the claim and the registration; then there is nothing to abort.
        if (!await context.SubmissionFileSummaries.AnyAsync(item => item.Id == candidate.Id &&
                item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc == leaseUntil, cancellationToken))
            return true;

        Result<Generated> result;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancelledByReviewer))
        {
            timeout.CancelAfter(settings.JobTimeout);
            try
            {
                result = await GenerateAsync(candidate.SubmissionFileId, candidate.CheckpointId, timeout.Token);
            }
            catch (OperationCanceledException) when (cancelledByReviewer.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // The reviewer's request already marked the row Cancelled; nothing to save.
                logger.LogInformation("File summary {SummaryId} was cancelled by a reviewer.", candidate.Id);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryUnavailable, "The summary took too long.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Only the type is logged: messages from parsers or the model client can contain document text.
                logger.LogError("File summary {SummaryId} crashed with {ExceptionType}.", candidate.Id, exception.GetType().Name);
                result = Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryUnavailable, "The summary failed unexpectedly.");
            }
        }

        if (result.IsSuccess)
        {
            var finishedAt = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
            await context.SubmissionFileSummaries
                .Where(item => item.Id == candidate.Id && item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc == leaseUntil)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, CheckpointSummaryStatus.Ready)
                    .SetProperty(item => item.ContentJson, result.Value.ContentJson)
                    .SetProperty(item => item.InputTruncated, result.Value.InputTruncated)
                    .SetProperty(item => item.Model, summarizer.ModelName)
                    .SetProperty(item => item.PromptVersion, CheckpointSummaryPrompt.Version)
                    .SetProperty(item => item.GeneratedAtUtc, finishedAt)
                    .SetProperty(item => item.AttemptCount, 0)
                    .SetProperty(item => item.NextAttemptAtUtc, (DateTime?)null)
                    .SetProperty(item => item.LastErrorCode, (string?)null), cancellationToken);
            logger.LogInformation("File summary {SummaryId} is ready.", candidate.Id);
            return true;
        }

        await RecordFailureAsync(candidate.Id, candidate.AttemptCount + 1, leaseUntil, result.Error, nowUtc, cancellationToken);
        return true;
    }

    private async Task<Result<Generated>> GenerateAsync(Guid fileId, Guid checkpointId, CancellationToken cancellationToken)
    {
        var checkpoint = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item => item.Id == checkpointId, cancellationToken);
        // The query filter hides deleted files: a file removed after it was queued has nothing left to summarise.
        var file = await context.SubmissionFiles.AsNoTracking()
            .Where(item => item.Id == fileId)
            .Select(item => new SummarySourceFile(item.Id, item.VersionNumber, item.OriginalName, item.FileSize, item.UploadedAt,
                item.StorageProvider, item.StorageKey, item.FileUrl))
            .FirstOrDefaultAsync(cancellationToken);
        if (checkpoint is null || file is null)
            return Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryNoContent, "The file is no longer part of this checkpoint.");
        var requirements = CheckpointSummarySource.Requirements(checkpoint);

        // One character over the limit tells a document that fills it exactly apart from a longer one.
        var read = await ReadTextAsync(file, settings.MaxInputCharacters + 1, cancellationToken);
        if (read.IsFailure) return Result.Failure<Generated>(read.Error);
        var text = CheckpointSummaryText.Redact(read.Value).Trim();
        var truncated = text.Length > settings.MaxInputCharacters;
        if (truncated) text = text[..settings.MaxInputCharacters];
        if (text.Length == 0) return Result.Failure<Generated>(Unreadable().Error);

        // Long files are condensed to their most informative sentences without a model, so one call reads them.
        var condensed = text.Length > settings.FilePromptCharacters;
        var prompt = condensed
            ? CheckpointSummaryText.Select(text, requirements.Select(item => item.Title).ToArray(), settings.FilePromptCharacters)
            : text;
        var answer = await summarizer.CompleteJsonAsync(CheckpointSummaryPrompt.System,
            CheckpointSummaryPrompt.User(checkpoint.CheckpointNumber, checkpoint.Name, requirements, file.OriginalName, prompt, condensed),
            CheckpointSummaryPrompt.Schema(requirements.Count), cancellationToken);
        if (answer.IsFailure) return Result.Failure<Generated>(answer.Error);

        // Quotes and highlights come from everything that was read, not only from the condensed prompt.
        var content = CheckpointSummaryOutput.TryBuild(answer.Value, text, requirements);
        if (content is null)
            return Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryUnavailable, "The model returned an answer that could not be used.");
        return Result.Success(new Generated(JsonSerializer.Serialize(content, JsonOptions), truncated || condensed));
    }

    private async Task<Result<string>> ReadTextAsync(SummarySourceFile file, int maxCharacters, CancellationToken cancellationToken)
    {
        if (file.FileSize > CheckpointSummaryLimits.MaxFileSizeBytes) return Unreadable();
        Result<byte[]> bytes;
        if (file.StorageProvider == SubmissionStorageProvider.R2)
        {
            if (string.IsNullOrWhiteSpace(file.StorageKey)) return Unreadable();
            bytes = await objectStorage.DownloadAsync(file.StorageKey, cancellationToken);
        }
        else
        {
            var download = await legacyStorage.DownloadAsync(file.FileUrl, cancellationToken);
            bytes = download.IsSuccess ? Result.Success(download.Value.Content) : Result.Failure<byte[]>(download.Error);
        }
        if (bytes.IsFailure)
        {
            // A missing object will not come back; storage outages are retried.
            return bytes.Error.Code == ErrorCodes.CommonNotFoundError
                ? Unreadable()
                : Result.Failure<string>(ErrorCodes.WorkspaceSummaryUnavailable, "Submitted files could not be downloaded.");
        }
        var extension = Path.GetExtension(file.OriginalName).ToLowerInvariant();
        return await extractor.ExtractAsync(bytes.Value, extension, maxCharacters, cancellationToken);
    }

    private async Task RecordFailureAsync(
        Guid summaryId, int attempts, DateTime leaseUntil, Error error, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Missing or unreadable content will not improve with retries; an unavailable model or storage might.
        var permanent = error.Code is ErrorCodes.WorkspaceSummaryNoContent or ErrorCodes.WorkspaceSummaryUnreadable;
        var failed = permanent || attempts >= CheckpointSummaryLimits.MaxAttempts;
        var delays = CheckpointSummaryLimits.RetryDelays;
        var nextAttempt = nowUtc + delays[Math.Min(attempts - 1, delays.Length - 1)];

        await context.SubmissionFileSummaries
            .Where(item => item.Id == summaryId && item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc == leaseUntil)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, failed ? CheckpointSummaryStatus.Failed : CheckpointSummaryStatus.Pending)
                .SetProperty(item => item.AttemptCount, attempts)
                .SetProperty(item => item.NextAttemptAtUtc, failed ? (DateTime?)null : nextAttempt)
                .SetProperty(item => item.LastErrorCode, error.Code), cancellationToken);

        logger.LogWarning("File summary {SummaryId} failed (attempt {Attempt}, {Outcome}): {Code}.",
            summaryId, attempts, failed ? "giving up" : "will retry", error.Code);
    }

    private static Result<string> Unreadable() => Result.Failure<string>(ErrorCodes.WorkspaceSummaryUnreadable,
        "The file has no readable text. Scanned PDFs, images and password-protected files are not supported.");
}
