using EHub.Application.Features.Showcase.Reports;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

// Bounded round-robin scans also catch Project Profile edits without adding hooks to other modules.
internal sealed class ShowcaseAutoReportService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ShowcaseAutoReportService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Showcase:AutoReport:Enabled", true)) return;
        var seconds = Math.Clamp(configuration.GetValue("Showcase:AutoReport:IntervalSeconds", 30), 5, 3600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        var pageOffset = 0;
        var commentOffset = 0;
        // Give startup migrations and reference-data initialization time to finish.
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var scanner = scope.ServiceProvider.GetRequiredService<IShowcaseAutoReportScanner>();
                var pages = await db.ShowcaseProjects.AsNoTracking()
                    .Where(x => x.Status == ShowcaseStatus.Published || x.Status == ShowcaseStatus.PendingReview)
                    .OrderBy(x => x.Id).Skip(pageOffset).Take(50).Select(x => x.Id).ToListAsync(stoppingToken);
                foreach (var id in pages) { await scanner.ScanPageAsync(id, stoppingToken); db.ChangeTracker.Clear(); }
                pageOffset = pages.Count < 50 ? 0 : pageOffset + 50;
                var comments = await db.ShowcaseComments.AsNoTracking().Where(x => x.ShowcaseProject.Status == ShowcaseStatus.Published)
                    .OrderBy(x => x.Id).Skip(commentOffset).Take(100).Select(x => x.Id).ToListAsync(stoppingToken);
                foreach (var id in comments) { await scanner.ScanCommentAsync(id, stoppingToken); db.ChangeTracker.Clear(); }
                commentOffset = comments.Count < 100 ? 0 : commentOffset + 100;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Showcase automatic content review scan failed; will retry next interval"); }
        }
    }
}
