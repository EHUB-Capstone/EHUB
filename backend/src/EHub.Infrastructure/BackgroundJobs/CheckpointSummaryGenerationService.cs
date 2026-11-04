using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

/// <summary>
/// Works through queued AI checkpoint summaries one at a time, so the local model never runs two requests at once.
/// Only registered when <c>Summarization:Provider</c> is enabled.
/// </summary>
internal sealed class CheckpointSummaryGenerationService(
    IServiceScopeFactory scopeFactory,
    ICheckpointSummaryQueueSignal queueSignal,
    ILogger<CheckpointSummaryGenerationService> logger) : BackgroundService
{
    // A new request wakes the worker at once; this poll only covers retries and other API instances.
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WarmUpAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = IdleDelay;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var generator = scope.ServiceProvider.GetRequiredService<ICheckpointSummaryGenerator>();
                var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().UtcNow;
                if (await generator.ProcessNextAsync(DateTime.SpecifyKind(now, DateTimeKind.Utc), stoppingToken)) delay = TimeSpan.Zero;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("Checkpoint summary loop failed with {ExceptionType}.", exception.GetType().Name);
                delay = ErrorDelay;
            }

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    if (delay == IdleDelay) await queueSignal.WaitAsync(IdleDelay, stoppingToken);
                    else await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    // Loads the model when the service starts, so the first summary does not wait for it.
    private async Task WarmUpAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICheckpointSummarizer>().WarmUpAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("Checkpoint summary model warm-up failed with {ExceptionType}.", exception.GetType().Name);
        }
    }
}
