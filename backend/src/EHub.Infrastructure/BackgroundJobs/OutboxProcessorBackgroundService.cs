using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class OutboxProcessorBackgroundService : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaximumAttempts = 10;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                var messageIds = await ClaimBatchAsync(stoppingToken);
                foreach (var messageId in messageIds)
                {
                    await ProcessAsync(messageId, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox polling cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task<IReadOnlyCollection<Guid>> ClaimBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var staleCutoff = now.Subtract(ProcessingLease);

        var messages = await context.OutboxMessages
            .FromSqlInterpolated($$"""
                SELECT *
                FROM outbox_messages
                WHERE (status = 'Pending' AND available_at_utc <= {{now}})
                   OR (status = 'Processing' AND processing_started_at_utc <= {{staleCutoff}})
                ORDER BY occurred_at_utc
                FOR UPDATE SKIP LOCKED
                LIMIT {{BatchSize}}
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
            message.ProcessingStartedAtUtc = now;
            message.AttemptCount++;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages.Select(message => message.Id).ToArray();
    }

    private async Task ProcessAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxEventDispatcher>();
        var message = await context.OutboxMessages
            .FirstOrDefaultAsync(candidate => candidate.Id == messageId, cancellationToken);
        if (message == null || message.Status != OutboxMessageStatus.Processing)
        {
            return;
        }

        try
        {
            await dispatcher.DispatchAsync(message, cancellationToken);
            message.Status = OutboxMessageStatus.Processed;
            message.ProcessedAtUtc = DateTime.UtcNow;
            message.ProcessingStartedAtUtc = null;
            message.LastError = null;
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Processed outbox event {OutboxEventId} {OutboxEventType}",
                message.EventId,
                message.Type);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reachedAttemptLimit = message.AttemptCount >= MaximumAttempts;
            message.Status = reachedAttemptLimit
                ? OutboxMessageStatus.Failed
                : OutboxMessageStatus.Pending;
            message.AvailableAtUtc = DateTime.UtcNow.AddSeconds(
                Math.Pow(2, Math.Min(message.AttemptCount, 8)));
            message.ProcessingStartedAtUtc = null;
            message.LastError = exception.ToString()[..Math.Min(exception.ToString().Length, 2_000)];
            await context.SaveChangesAsync(cancellationToken);

            if (reachedAttemptLimit)
            {
                _logger.LogError(
                    exception,
                    "Outbox event {OutboxEventId} {OutboxEventType} failed permanently after {AttemptCount} attempts",
                    message.EventId,
                    message.Type,
                    message.AttemptCount);
            }
            else
            {
                _logger.LogWarning(
                    exception,
                    "Outbox event {OutboxEventId} {OutboxEventType} failed on attempt {AttemptCount}; retry scheduled at {RetryAtUtc}",
                    message.EventId,
                    message.Type,
                    message.AttemptCount,
                    message.AvailableAtUtc);
            }
        }
    }
}
