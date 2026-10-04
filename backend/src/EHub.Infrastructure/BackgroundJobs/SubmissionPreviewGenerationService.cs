using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

/// <summary>Generates PDF previews for newly uploaded DOCX/PPTX files, one file at a time.</summary>
internal sealed class SubmissionPreviewGenerationService(
    IServiceScopeFactory scopeFactory,
    ILogger<SubmissionPreviewGenerationService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = IdleDelay;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = new SubmissionPreviewGenerationRunner(
                    scope.ServiceProvider.GetRequiredService<AppDbContext>(),
                    scope.ServiceProvider.GetRequiredService<ICheckpointPreviewGenerator>(),
                    logger);
                var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().UtcNow;
                // Keep going without waiting while there is queued work.
                if (await runner.ProcessNextAsync(now, stoppingToken)) delay = TimeSpan.Zero;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Submission preview generation loop failed");
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
