using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class PendingSubmissionUploadCleanupService(
    IServiceScopeFactory scopeFactory,
    ISubmissionObjectStorage storage,
    ILogger<PendingSubmissionUploadCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupAsync(stoppingToken);
        using var timer = new PeriodicTimer(CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupAsync(stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().UtcNow;
            var cleaner = new SubmissionUploadSessionCleaner(context, storage, logger);

            var expired = await cleaner.ExpireAbandonedSessionsAsync(now, cancellationToken);
            var purged = await cleaner.PurgeFinishedSessionsAsync(now, cancellationToken);
            if (expired > 0 || purged > 0)
            {
                logger.LogInformation(
                    "Submission upload cleanup expired {Expired} abandoned sessions and purged {Purged} finished sessions.",
                    expired, purged);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Submission upload cleanup failed");
        }
    }
}
