using EHub.Application.Features.Showcase.Upvote;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Infrastructure.Persistence.Repositories;

public sealed class ShowcaseUpvoteStore(AppDbContext context) : IShowcaseUpvoteStore
{
    public async Task<ShowcaseProject?> LockPublishedAsync(Guid showcaseId, CancellationToken cancellationToken)
    {
        var published = ShowcaseStatus.Published.ToString();
        // Take the showcase lock before touching any vote rows. This also serializes with hide/withdraw updates.
        var rows = await context.ShowcaseProjects
            .FromSqlInterpolated($"""
                SELECT showcase.*, showcase.xmin
                FROM showcase_projects AS showcase
                WHERE showcase.id = {showcaseId} AND showcase.status = {published} AND NOT showcase.is_deleted
                FOR NO KEY UPDATE
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    public async Task<bool> TryAddAsync(Guid showcaseId, Guid userId, DateTime createdAt, CancellationToken cancellationToken)
    {
        var inserted = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO showcase_upvotes (showcase_project_id, user_id, created_at)
            VALUES ({showcaseId}, {userId}, {createdAt})
            ON CONFLICT (showcase_project_id, user_id) DO NOTHING
            """, cancellationToken);

        return inserted == 1;
    }
}
