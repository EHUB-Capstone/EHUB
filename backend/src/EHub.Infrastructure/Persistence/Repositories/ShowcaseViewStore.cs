using EHub.Application.Features.Showcase.Views;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Infrastructure.Persistence.Repositories;

public sealed class ShowcaseViewStore(AppDbContext context) : IShowcaseViewStore
{
    public async Task<int?> RecordAsync(Guid showcaseId, Guid userId, DateOnly dateUtc, CancellationToken cancellationToken)
    {
        var status = ShowcaseStatus.Published.ToString();
        // Lock the published row before inserting. The insert and counter update are one atomic statement.
        var counts = await context.Database.SqlQuery<int>($"""
            WITH published AS (
                SELECT id, view_count
                FROM showcase_projects
                WHERE id = {showcaseId} AND status = {status} AND NOT is_deleted
                FOR NO KEY UPDATE
            ), inserted AS (
                INSERT INTO showcase_view_daily (showcase_project_id, user_id, view_date_utc)
                SELECT id, {userId}, {dateUtc} FROM published
                ON CONFLICT (showcase_project_id, user_id, view_date_utc) DO NOTHING
                RETURNING showcase_project_id
            ), incremented AS (
                UPDATE showcase_projects AS showcase
                SET view_count = showcase.view_count + 1
                FROM inserted
                WHERE showcase.id = inserted.showcase_project_id
                RETURNING showcase.view_count
            )
            SELECT COALESCE((SELECT view_count FROM incremented), published.view_count) AS "Value"
            FROM published
            """).ToListAsync(cancellationToken);

        return counts.Count == 0 ? null : counts.Single();
    }
}
