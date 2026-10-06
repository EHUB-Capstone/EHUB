using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

/// <summary>
/// Converts one queued DOCX/PPTX submission file to its cached PDF preview. Files are claimed with a
/// short lease so overlapping runs never convert the same file twice, and failures are retried with
/// a back-off before the file is marked Failed. Safe to call repeatedly and from several instances.
/// </summary>
internal sealed class SubmissionPreviewGenerationRunner(
    AppDbContext context,
    ICheckpointPreviewGenerator generator,
    ILogger logger)
{
    /// <returns>True when a file was handled (or lost to another worker), so the caller should poll again immediately.</returns>
    public async Task<bool> ProcessNextAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var candidate = await context.SubmissionFiles.AsNoTracking()
            .Where(file => file.PreviewStatus == SubmissionPreviewStatus.Pending &&
                (file.PreviewNextAttemptAtUtc == null || file.PreviewNextAttemptAtUtc <= nowUtc))
            .OrderBy(file => file.PreviewNextAttemptAtUtc)
            .Select(file => new
            {
                file.Id,
                file.Submission.TeamId,
                file.Submission.CheckpointId,
                file.PreviewAttemptCount
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return false;

        var leaseUntil = nowUtc + SubmissionFileLimits.PreviewGenerationLease;
        var claimed = await context.SubmissionFiles
            .Where(file => file.Id == candidate.Id &&
                file.PreviewStatus == SubmissionPreviewStatus.Pending &&
                (file.PreviewNextAttemptAtUtc == null || file.PreviewNextAttemptAtUtc <= nowUtc))
            .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.PreviewNextAttemptAtUtc, leaseUntil), cancellationToken);
        if (claimed == 0) return true;

        var checkpointNumber = await context.Checkpoints.AsNoTracking()
            .Where(checkpoint => checkpoint.Id == candidate.CheckpointId)
            .Select(checkpoint => checkpoint.CheckpointNumber)
            .FirstOrDefaultAsync(cancellationToken);

        Result<GeneratedPreview> result;
        try
        {
            result = await generator.EnsurePreviewAsync(candidate.Id, candidate.TeamId, checkpointNumber, needContent: false, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Background preview generation crashed for file {FileId}.", candidate.Id);
            result = Result.Failure<GeneratedPreview>(ErrorCodes.WorkspaceFilePreviewUnavailable, "Preview generation failed unexpectedly.");
        }

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Background preview ready for file {FileId}: cache {Cache}, storage {StorageMs} ms, convert {ConvertMs} ms.",
                candidate.Id, result.Value.FromCache ? "hit" : "miss", result.Value.Timings.StorageMs, result.Value.Timings.ConvertMs);
            return true;
        }

        await RecordFailureAsync(candidate.Id, candidate.PreviewAttemptCount + 1, result.Error, nowUtc, cancellationToken);
        return true;
    }

    private async Task RecordFailureAsync(Guid fileId, int attempts, Error error, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // A corrupt or unsupported document will not get better with retries; an unavailable converter or storage might.
        var permanent = error.Code is ErrorCodes.WorkspaceFilePreviewConversionFailed
            or ErrorCodes.WorkspaceFilePreviewUnsupported
            or ErrorCodes.CommonNotFoundError;
        var failed = permanent || attempts >= SubmissionFileLimits.MaxPreviewGenerationAttempts;
        var retryDelays = SubmissionFileLimits.PreviewGenerationRetryDelays;
        var nextAttempt = nowUtc + retryDelays[Math.Min(attempts - 1, retryDelays.Length - 1)];
        var message = $"{error.Code}: {error.Message}";
        if (message.Length > 500) message = message[..500];

        await context.SubmissionFiles
            .Where(file => file.Id == fileId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(file => file.PreviewStatus, failed ? SubmissionPreviewStatus.Failed : SubmissionPreviewStatus.Pending)
                .SetProperty(file => file.PreviewAttemptCount, attempts)
                .SetProperty(file => file.PreviewNextAttemptAtUtc, failed ? (DateTime?)null : nextAttempt)
                .SetProperty(file => file.PreviewLastError, message), cancellationToken);

        logger.LogWarning(
            "Background preview generation failed for file {FileId} (attempt {Attempt}, {Outcome}): {Code}.",
            fileId, attempts, failed ? "giving up" : "will retry", error.Code);
    }
}
