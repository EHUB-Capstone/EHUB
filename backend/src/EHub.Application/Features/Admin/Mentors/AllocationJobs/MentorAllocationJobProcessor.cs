using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Mentors;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Application.Features.Admin.Mentors.AllocationJobs;

public sealed class MentorAllocationJobProcessor(IApplicationDbContext db, MentorAdminHandler previews,
    MentorRankingExecution execution, IDateTimeProvider clock, ILogger<MentorAllocationJobProcessor> logger,
    IOptions<MentorAllocationOptions>? allocationOptions = null)
    : IMentorAllocationJobProcessor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<(Guid Id, Guid LeaseId)?> ClaimAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        await db.MentorAllocationJobs.Where(item => item.Attempts >= 3 &&
            (item.Status == MentorAllocationJobStatus.Queued ||
                item.Status == MentorAllocationJobStatus.Processing && item.LeaseExpiresAtUtc <= now)).ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, MentorAllocationJobStatus.Failed)
                .SetProperty(item => item.ErrorMessage, "The allocation task was interrupted repeatedly. Please try again."), ct);
        var candidate = await db.MentorAllocationJobs.AsNoTracking().Where(item => item.ExpiresAtUtc > now &&
            item.Attempts < 3 && (item.Status == MentorAllocationJobStatus.Queued ||
                item.Status == MentorAllocationJobStatus.Processing && item.LeaseExpiresAtUtc <= now))
            .OrderBy(item => item.CreatedAtUtc).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(ct);
        if (candidate is null) return null;
        var leaseId = Guid.NewGuid();
        var claimed = await db.MentorAllocationJobs.Where(item => item.Id == candidate && item.ExpiresAtUtc > now &&
            item.Attempts < 3 && (item.Status == MentorAllocationJobStatus.Queued ||
                item.Status == MentorAllocationJobStatus.Processing && item.LeaseExpiresAtUtc <= now))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, MentorAllocationJobStatus.Processing)
                .SetProperty(item => item.LeaseId, leaseId).SetProperty(item => item.LeaseExpiresAtUtc, now.AddMinutes(2))
                .SetProperty(item => item.Attempts, item => item.Attempts + 1)
                .SetProperty(item => item.CompletedBatches, 0).SetProperty(item => item.TotalBatches, 0)
                .SetProperty(item => item.ErrorMessage, (string?)null), ct);
        return claimed == 1 ? (candidate.Value, leaseId) : null;
    }

    public async Task<bool> RenewAsync(Guid id, Guid leaseId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        return await db.MentorAllocationJobs.Where(item => item.Id == id && item.LeaseId == leaseId &&
                item.Status == MentorAllocationJobStatus.Processing && item.LeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.LeaseExpiresAtUtc, now.AddMinutes(2)), ct) == 1;
    }

    public async Task ProcessAsync(Guid id, Guid leaseId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var job = await db.MentorAllocationJobs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id &&
            item.LeaseId == leaseId && item.Status == MentorAllocationJobStatus.Processing && item.LeaseExpiresAtUtc > now, ct);
        if (job is null) return;
        var owned = db.MentorAllocationJobs.Where(item => item.Id == id && item.LeaseId == leaseId &&
            item.Status == MentorAllocationJobStatus.Processing);
        var completedBatches = 0;
        var totalBatches = 0;
        try
        {
            if (!await IsAdminAsync(job.AdminUserId, ct)) throw new InvalidOperationException("Administrator access changed.");
            var request = JsonSerializer.Deserialize<PreviewMentorAllocationRequest>(job.RequestJson, Json)
                ?? throw new JsonException();
            execution.EnableBackground(async (completed, total, token) =>
            {
                var currentTime = clock.UtcNow;
                if (await owned.Where(item => item.LeaseExpiresAtUtc > currentTime).ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.CompletedBatches, completed).SetProperty(item => item.TotalBatches, total), token) != 1)
                    throw new OperationCanceledException("The allocation task lease ended.");
                completedBatches = completed;
                totalBatches = total;
            });
            var settings = allocationOptions?.Value ?? new MentorAllocationOptions();
            Result<MentorAllocationPreviewResponse> result;
            for (var retry = 0; ; retry++)
            {
                result = await previews.PreviewAllocationForAdminAsync(request, job.AdminUserId, ct);
                if (result.IsSuccess || retry >= settings.MaxProviderRetries || !IsTemporaryProviderProblem(previews.LastRankingFailure)) break;
                // The models are cooled down for a minute after a failure; waiting past it lets the retry succeed, and batches
                // that already finished are served from the ranker's cache. The worker keeps the lease alive meanwhile.
                logger.LogWarning("Mentor allocation task {JobId} hit a temporary AI problem ({FailureKind}); retry {Retry}/{MaxRetries} in {Seconds} seconds",
                    id, previews.LastRankingFailure, retry + 1, settings.MaxProviderRetries, settings.ProviderRetryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(settings.ProviderRetryDelaySeconds), ct);
            }
            ct.ThrowIfCancellationRequested();
            if (!await IsAdminAsync(job.AdminUserId, ct)) throw new InvalidOperationException("Administrator access changed.");
            now = clock.UtcNow;
            var resultJson = result.IsSuccess ? JsonSerializer.Serialize(result.Value, Json) : null;
            var message = result.IsSuccess ? null : result.Error.Message;
            if (result.IsFailure)
                logger.LogWarning("Mentor allocation task {JobId} failed with {ErrorCode}; completed {CompletedBatches}/{TotalBatches} batches",
                    id, result.Error.Code, completedBatches, totalBatches);
            await owned.Where(item => item.LeaseExpiresAtUtc > now).ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, result.IsSuccess ? MentorAllocationJobStatus.Ready : MentorAllocationJobStatus.Failed)
                .SetProperty(item => item.ResultJson, resultJson).SetProperty(item => item.ErrorMessage, message)
                .SetProperty(item => item.LeaseId, (Guid?)null).SetProperty(item => item.LeaseExpiresAtUtc, (DateTime?)null), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await owned.ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, MentorAllocationJobStatus.Queued)
                .SetProperty(item => item.LeaseId, (Guid?)null).SetProperty(item => item.LeaseExpiresAtUtc, (DateTime?)null),
                CancellationToken.None);
        }
        catch (Exception error)
        {
            logger.LogWarning("Mentor allocation task {JobId} failed with {ExceptionType}; completed {CompletedBatches}/{TotalBatches} batches",
                id, error.GetType().Name, completedBatches, totalBatches);
            await owned.ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, MentorAllocationJobStatus.Failed)
                .SetProperty(item => item.ErrorMessage, "AI allocation could not finish. Please try again.")
                .SetProperty(item => item.LeaseId, (Guid?)null).SetProperty(item => item.LeaseExpiresAtUtc, (DateTime?)null),
                CancellationToken.None);
        }
    }

    // Authentication, permission, quota, configuration and input problems do not go away by waiting, so they are not retried.
    private static bool IsTemporaryProviderProblem(MentorFitFailure? failure) => failure is MentorFitFailure.ServiceUnavailable or
        MentorFitFailure.Timeout or MentorFitFailure.Network or MentorFitFailure.InvalidResponse;

    private Task<bool> IsAdminAsync(Guid adminId, CancellationToken ct) => db.Users.AnyAsync(user => user.Id == adminId &&
        user.Status == UserStatus.Active && user.UserRoles.Any(role => role.Role.Name == SystemRoles.Admin), ct);
}
