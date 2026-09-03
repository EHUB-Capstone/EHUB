using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class LecturerImportSessionCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ConsumedRetention = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LecturerImportSessionCleanupService> _logger;

    public LecturerImportSessionCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<LecturerImportSessionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);
        do
        {
            await CleanupAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var consumedCutoff = now.Subtract(ConsumedRetention);
            var deleted = await context.LecturerImportSessions
                .Where(session =>
                    session.ExpiresAtUtc <= now ||
                    (session.Status == LecturerImportSessionStatus.Consumed &&
                     session.ConsumedAtUtc <= consumedCutoff))
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Removed {LecturerImportSessionCount} expired or retained lecturer import sessions",
                    deleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Lecturer import session cleanup failed");
        }
    }
}
