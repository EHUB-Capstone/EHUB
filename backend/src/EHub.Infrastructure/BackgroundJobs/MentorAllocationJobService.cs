using EHub.Application.Features.Admin.Mentors.AllocationJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class MentorAllocationJobService(IServiceScopeFactory scopes, ILogger<MentorAllocationJobService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var claimScope = scopes.CreateAsyncScope();
                var claim = await claimScope.ServiceProvider.GetRequiredService<IMentorAllocationJobProcessor>().ClaimAsync(stoppingToken);
                if (claim is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }
                await using var workScope = scopes.CreateAsyncScope();
                using var run = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var heartbeat = KeepLeaseAsync(claim.Value.Id, claim.Value.LeaseId, run);
                try
                {
                    await workScope.ServiceProvider.GetRequiredService<IMentorAllocationJobProcessor>()
                        .ProcessAsync(claim.Value.Id, claim.Value.LeaseId, run.Token);
                }
                finally
                {
                    await run.CancelAsync();
                    await heartbeat;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                logger.LogWarning("Mentor allocation worker failed with {ExceptionType}", error.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task KeepLeaseAsync(Guid id, Guid leaseId, CancellationTokenSource run)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
            while (await timer.WaitForNextTickAsync(run.Token))
            {
                await using var scope = scopes.CreateAsyncScope();
                if (!await scope.ServiceProvider.GetRequiredService<IMentorAllocationJobProcessor>()
                        .RenewAsync(id, leaseId, run.Token))
                {
                    await run.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested) { }
        catch (Exception error)
        {
            logger.LogWarning("Mentor allocation heartbeat failed with {ExceptionType}", error.GetType().Name);
            await run.CancelAsync();
        }
    }
}
