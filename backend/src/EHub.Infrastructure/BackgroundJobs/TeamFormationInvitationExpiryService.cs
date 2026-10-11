using EHub.Application.Features.Teams.TeamFormations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class TeamFormationInvitationExpiryService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TeamFormationInvitationExpiryService> _logger;

    public TeamFormationInvitationExpiryService(
        IServiceScopeFactory scopeFactory,
        ILogger<TeamFormationInvitationExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await ExpireAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExpireAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ITeamFormationExpirationService>();
            var expired = await service.ExpireDueAsync(cancellationToken);
            if (expired > 0)
                _logger.LogInformation("Expired {ExpiredInvitationCount} team formation invitations", expired);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Team formation invitation expiry failed");
        }
    }
}
