using EHub.Application.Common.Interfaces.Storage;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

/// <summary>
/// Removes direct-upload sessions that never completed together with their orphaned R2 objects.
/// Safe to run repeatedly and from several instances: a session only becomes Expired once its
/// object is confirmed gone, so a failed delete is simply retried on the next run.
/// </summary>
internal sealed class SubmissionUploadSessionCleaner(
    AppDbContext context,
    ISubmissionObjectStorage storage,
    ILogger logger)
{
    private const int BatchSize = 50;
    private const int MaximumBatchesPerRun = 20;

    public async Task<int> ExpireAbandonedSessionsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Complete is refused after ExpiresAtUtc, so waiting a grace period guarantees no completion is in flight.
        var cutoff = nowUtc - SubmissionFileLimits.UploadCleanupGrace;
        var expired = 0;
        for (var batchNumber = 0; batchNumber < MaximumBatchesPerRun; batchNumber++)
        {
            var batch = await context.SubmissionUploadSessions
                .Where(item => item.Status == SubmissionUploadSessionStatus.Pending && item.ExpiresAtUtc < cutoff)
                .OrderBy(item => item.ExpiresAtUtc)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0) break;

            var progressed = 0;
            foreach (var session in batch)
            {
                if (!await ObjectIsGoneAsync(session.ObjectKey, cancellationToken)) continue;
                session.Status = SubmissionUploadSessionStatus.Expired;
                progressed++;
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another instance changed a session first; the next query sees its new state.
                context.ChangeTracker.Clear();
                continue;
            }

            expired += progressed;
            if (progressed == 0) break;
        }

        return expired;
    }

    /// <summary>Finished sessions are kept briefly so a retried complete still resolves, then deleted.</summary>
    public Task<int> PurgeFinishedSessionsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var cutoff = nowUtc - SubmissionFileLimits.UploadSessionRetention;
        return context.SubmissionUploadSessions
            .Where(item => item.Status != SubmissionUploadSessionStatus.Pending && item.CreatedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<bool> ObjectIsGoneAsync(string objectKey, CancellationToken cancellationToken)
    {
        await storage.DeleteAsync(objectKey, cancellationToken);
        var remaining = await storage.GetObjectInfoAsync(objectKey, cancellationToken);
        if (remaining.IsFailure && remaining.Error.Code == ErrorCodes.CommonNotFoundError) return true;

        logger.LogWarning("Abandoned upload object {ObjectKey} could not be removed yet; it will be retried.", objectKey);
        return false;
    }
}
