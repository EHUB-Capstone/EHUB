using EHub.Application.Common.Interfaces.AI;
using EHub.Application.Features.ProposalAnalyses;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class ProjectProposalAnalysisWorker : BackgroundService
{
    private const int BatchSize = 5;
    private const int MaximumAttempts = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAiFeatureGate _featureGate;
    private readonly ILogger<ProjectProposalAnalysisWorker> _logger;

    public ProjectProposalAnalysisWorker(
        IServiceScopeFactory scopeFactory,
        IAiFeatureGate featureGate,
        ILogger<ProjectProposalAnalysisWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _featureGate = featureGate;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Project proposal analysis polling cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task<int> RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!_featureGate.IsEnabled) return 0;

        var jobs = await ClaimBatchAsync(cancellationToken);
        foreach (var job in jobs)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IProjectProposalAnalysisJobProcessor>();
                await processor.ProcessAsync(job.JobId, job.LeaseOwner, cancellationToken);
                _logger.LogInformation("Completed project proposal analysis job {AnalysisJobId}", job.JobId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await RecordFailureAsync(job, exception, cancellationToken);
            }
        }

        return jobs.Count;
    }

    private async Task<IReadOnlyCollection<ClaimedJob>> ClaimBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        var jobs = await context.ProjectProposalAnalysisJobs
            .FromSqlInterpolated($$"""
                SELECT *
                FROM project_proposal_analysis_jobs
                WHERE (status = 'Pending' AND available_at_utc <= {{now}})
                   OR (status = 'Processing' AND (lease_expires_at_utc IS NULL OR lease_expires_at_utc <= {{now}}))
                ORDER BY created_at_utc
                FOR UPDATE SKIP LOCKED
                LIMIT {{BatchSize}}
                """)
            .ToListAsync(cancellationToken);

        var claimed = new List<ClaimedJob>(jobs.Count);
        foreach (var job in jobs)
        {
            var leaseOwner = Guid.NewGuid().ToString("N");
            job.Status = ProjectProposalAnalysisJobStatus.Processing;
            job.ProcessingStartedAtUtc = now;
            job.LeaseOwner = leaseOwner;
            job.LeaseExpiresAtUtc = now.Add(ProcessingLease);
            job.AttemptCount++;
            job.FailedAtUtc = null;
            claimed.Add(new ClaimedJob(job.Id, leaseOwner));
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    private async Task RecordFailureAsync(ClaimedJob claimedJob, Exception exception, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await context.ProjectProposalAnalysisJobs.SingleOrDefaultAsync(candidate =>
            candidate.Id == claimedJob.JobId
            && candidate.Status == ProjectProposalAnalysisJobStatus.Processing
            && candidate.LeaseOwner == claimedJob.LeaseOwner, cancellationToken);
        if (job == null) return;

        var processingFailure = exception as ProposalAnalysisProcessingException;
        var errorCode = processingFailure?.ErrorCode ?? "PROPOSAL_ANALYSIS_UNEXPECTED";
        var canRetry = processingFailure?.IsTransient != false && job.AttemptCount < MaximumAttempts;
        var now = DateTime.UtcNow;

        job.Status = canRetry ? ProjectProposalAnalysisJobStatus.Pending : ProjectProposalAnalysisJobStatus.Failed;
        job.AvailableAtUtc = canRetry
            ? now.AddSeconds(Math.Pow(2, Math.Min(job.AttemptCount, 8)))
            : job.AvailableAtUtc;
        job.FailedAtUtc = canRetry ? null : now;
        job.ProcessingStartedAtUtc = null;
        job.LeaseOwner = null;
        job.LeaseExpiresAtUtc = null;
        job.LastErrorCode = errorCode;
        await context.SaveChangesAsync(cancellationToken);

        if (canRetry)
        {
            _logger.LogWarning(
                "Project proposal analysis job {AnalysisJobId} failed on attempt {AttemptCount}; retry scheduled at {RetryAtUtc}: {ErrorCode}",
                job.Id,
                job.AttemptCount,
                job.AvailableAtUtc,
                errorCode);
        }
        else
        {
            _logger.LogError(
                "Project proposal analysis job {AnalysisJobId} failed permanently after {AttemptCount} attempts: {ErrorCode}",
                job.Id,
                job.AttemptCount,
                errorCode);
        }
    }

    private sealed record ClaimedJob(Guid JobId, string LeaseOwner);
}
