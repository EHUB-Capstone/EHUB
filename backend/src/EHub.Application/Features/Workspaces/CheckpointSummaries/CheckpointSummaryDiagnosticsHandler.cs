using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Contracts.Workspaces;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public interface ICheckpointSummaryDiagnosticsHandler
{
    Task<Result<WorkspaceCheckpointSummaryDiagnosticsResponse>> GetAsync(string role, CancellationToken cancellationToken = default);
}

/// <summary>
/// What an administrator needs to see when summaries are slow or missing: whether the model server is up and has its model, the
/// state of the circuit that pauses the worker, how long the queue is and why files failed. Aggregates only, no submission text.
/// </summary>
public sealed class CheckpointSummaryDiagnosticsHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    CheckpointSummarySettings settings,
    ICheckpointSummarizer summarizer,
    CheckpointSummaryCircuitBreaker? breaker = null) : ICheckpointSummaryDiagnosticsHandler
{
    private const int MaximumFailureCodes = 10;

    public async Task<Result<WorkspaceCheckpointSummaryDiagnosticsResponse>> GetAsync(string role, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<WorkspaceCheckpointSummaryDiagnosticsResponse>(ErrorCodes.WorkspaceAccessDenied, "Only administrators can see this.");
        if (!settings.Enabled)
            return Result.Failure<WorkspaceCheckpointSummaryDiagnosticsResponse>(ErrorCodes.WorkspaceSummaryDisabled, "AI summaries are not enabled on this server.");

        var now = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
        var health = await summarizer.CheckHealthAsync(cancellationToken);
        var snapshot = breaker?.Snapshot(now) ?? new CheckpointCircuitSnapshot(CheckpointCircuitState.Closed, 0, null, null);

        var groups = await context.SubmissionFileSummaries.AsNoTracking()
            .GroupBy(item => new { item.Status, item.IsAutomatic })
            .Select(group => new { group.Key.Status, group.Key.IsAutomatic, Count = group.Count() })
            .ToListAsync(cancellationToken);
        int Count(CheckpointSummaryStatus status, bool? automatic = null) =>
            groups.Where(group => group.Status == status && (automatic is null || group.IsAutomatic == automatic)).Sum(group => group.Count);

        var expired = await context.SubmissionFileSummaries.AsNoTracking()
            .CountAsync(item => item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc < now, cancellationToken);
        var oldest = await context.SubmissionFileSummaries.AsNoTracking()
            .Where(item => item.Status == CheckpointSummaryStatus.Pending)
            .MinAsync(item => (DateTime?)item.RequestedAtUtc, cancellationToken);
        var lastGenerated = await context.SubmissionFileSummaries.AsNoTracking()
            .MaxAsync(item => item.GeneratedAtUtc, cancellationToken);
        var failures = await context.SubmissionFileSummaries.AsNoTracking()
            .Where(item => item.Status == CheckpointSummaryStatus.Failed)
            .GroupBy(item => item.LastErrorCode ?? "UNKNOWN")
            .Select(group => new WorkspaceSummaryFailureCount { ErrorCode = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .Take(MaximumFailureCodes)
            .ToListAsync(cancellationToken);

        var verdicts = await context.CheckpointSummaryFeedbacks.AsNoTracking()
            .GroupBy(item => new { item.PromptVersion, item.Rating })
            .Select(group => new { group.Key.PromptVersion, group.Key.Rating, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var feedback = verdicts.GroupBy(item => item.PromptVersion ?? "unknown")
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new WorkspaceSummaryFeedbackCount
            {
                PromptVersion = group.Key,
                Helpful = group.Where(item => item.Rating == CheckpointSummaryFeedbackRating.Helpful).Sum(item => item.Count),
                NotHelpful = group.Where(item => item.Rating == CheckpointSummaryFeedbackRating.NotHelpful).Sum(item => item.Count)
            }).ToList();

        return Result.Success(new WorkspaceCheckpointSummaryDiagnosticsResponse
        {
            Enabled = true,
            Model = summarizer.ModelName,
            PromptVersion = CheckpointSummaryPrompt.Version,
            AutoSummarize = settings.AutoSummarize,
            ModelReachable = health.Reachable,
            ModelAvailable = health.ModelAvailable,
            Circuit = snapshot.State.ToString(),
            ConsecutiveModelFailures = snapshot.ConsecutiveFailures,
            CircuitOpenUntilUtc = snapshot.State == CheckpointCircuitState.Closed ? null : snapshot.OpenUntilUtc,
            LastModelFailureUtc = snapshot.LastFailureUtc,
            PendingRequested = Count(CheckpointSummaryStatus.Pending, automatic: false),
            PendingAutomatic = Count(CheckpointSummaryStatus.Pending, automatic: true),
            Processing = Count(CheckpointSummaryStatus.Processing),
            ExpiredLeases = expired,
            Ready = Count(CheckpointSummaryStatus.Ready),
            Failed = Count(CheckpointSummaryStatus.Failed),
            Cancelled = Count(CheckpointSummaryStatus.Cancelled),
            OldestPendingSeconds = oldest is null ? null : Math.Max(0, (long)(now - oldest.Value).TotalSeconds),
            LastGeneratedAtUtc = lastGenerated,
            FailuresByCode = failures,
            FeedbackByPromptVersion = feedback,
            MaxRequestedQueue = CheckpointSummaryLimits.MaxQueuedSummaries,
            MaxAutomaticQueue = CheckpointSummaryLimits.MaxQueuedAutomaticSummaries
        });
    }
}
