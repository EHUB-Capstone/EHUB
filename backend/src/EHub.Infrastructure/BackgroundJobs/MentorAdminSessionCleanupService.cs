using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class MentorAdminSessionCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<MentorAdminSessionCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-1);
                var importCount = await context.MentorImportSessions
                    .Where(item => item.ExpiresAtUtc < DateTime.UtcNow || item.Status == MentorAdminSessionStatus.Consumed && item.ConsumedAtUtc < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                var allocationCount = await context.MentorAllocationSessions
                    .Where(item => item.ExpiresAtUtc < DateTime.UtcNow || item.Status == MentorAdminSessionStatus.Consumed && item.ConsumedAtUtc < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                if (importCount + allocationCount > 0)
                    logger.LogInformation("Removed {MentorAdminSessionCount} expired mentor administration sessions", importCount + allocationCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to clean up mentor administration sessions");
            }
        }
    }
}
