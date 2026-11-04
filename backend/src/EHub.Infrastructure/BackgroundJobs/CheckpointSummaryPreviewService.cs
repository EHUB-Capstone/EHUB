using EHub.Application.Features.Workspaces.CheckpointSummaries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

/// <summary>
/// Builds the model-free quick view of queued files while the model is busy with another file. Only registered when
/// <c>Summarization:Provider</c> is enabled.
/// </summary>
internal sealed class CheckpointSummaryPreviewService(
    IServiceScopeFactory scopeFactory,
    ILogger<CheckpointSummaryPreviewService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = IdleDelay;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var previewer = scope.ServiceProvider.GetRequiredService<ICheckpointSummaryPreviewer>();
                if (await previewer.ProcessNextAsync(stoppingToken)) delay = TimeSpan.Zero;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("Checkpoint quick view loop failed with {ExceptionType}.", exception.GetType().Name);
                delay = ErrorDelay;
            }

            if (delay > TimeSpan.Zero)
            {
                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
