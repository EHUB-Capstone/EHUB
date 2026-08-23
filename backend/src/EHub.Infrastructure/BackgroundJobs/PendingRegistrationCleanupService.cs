using EHub.Application.Common.Models.Identity;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class PendingRegistrationCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RegistrationOtpOptions _options;
    private readonly ILogger<PendingRegistrationCleanupService> _logger;

    public PendingRegistrationCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<RegistrationOtpOptions> options,
        ILogger<PendingRegistrationCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

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
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cutoff = DateTime.UtcNow.AddHours(-Math.Max(1, _options.CleanupRetentionHours));
            var removed = await context.PendingRegistrations
                .IgnoreQueryFilters()
                .Where(item =>
                    (item.Status == PendingRegistrationStatus.Completed && item.CompletedAtUtc < cutoff) ||
                    (item.Status != PendingRegistrationStatus.Completed && item.OtpExpiresAtUtc < cutoff))
                .ExecuteDeleteAsync(cancellationToken);

            if (removed > 0)
            {
                _logger.LogInformation("Removed {Count} expired pending registrations", removed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Pending registration cleanup failed");
        }
    }
}
