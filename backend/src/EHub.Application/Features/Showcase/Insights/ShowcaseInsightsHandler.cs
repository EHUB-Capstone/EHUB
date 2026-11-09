using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Showcase;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Insights;

public sealed class ShowcaseInsightsHandler(IApplicationDbContext context, IDateTimeProvider time) : IShowcaseInsightsHandler
{
    public async Task<Result<ShowcaseInsightsDto>> GetAsync(Guid showcaseId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !await context.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
            return Result.Failure<ShowcaseInsightsDto>(ErrorCodes.CommonUnauthorizedError, "Authentication is required.");

        var showcase = await context.ShowcaseProjects.AsNoTracking()
            .Where(item => item.Id == showcaseId)
            .Select(item => new { item.ProjectLineageId, item.ViewCount, item.LikeCount, item.CommentCount })
            .SingleOrDefaultAsync(cancellationToken);
        if (showcase is null || !await ShowcaseAccess.OwnsLineageAsync(context, showcase.ProjectLineageId, userId, cancellationToken)) return NotFound();

        var end = DateOnly.FromDateTime(time.UtcNow);
        var start = end.AddDays(-13);
        var previousStart = start.AddDays(-14);
        var startInstant = previousStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusiveInstant = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var views = await context.ShowcaseViewsDaily.AsNoTracking()
            .Where(item => item.ShowcaseProjectId == showcaseId && item.ViewDateUtc >= previousStart && item.ViewDateUtc <= end)
            .GroupBy(item => item.ViewDateUtc).Select(group => new { Date = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Date, item => item.Count, cancellationToken);
        var upvotes = await context.ShowcaseUpvotes.AsNoTracking()
            .Where(item => item.ShowcaseProjectId == showcaseId && item.CreatedAt >= startInstant && item.CreatedAt < endExclusiveInstant)
            .Select(item => item.CreatedAt).ToListAsync(cancellationToken);
        var upvotesByDay = upvotes.GroupBy(value => DateOnly.FromDateTime(value)).ToDictionary(group => group.Key, group => group.Count());
        var comments = await context.ShowcaseComments.AsNoTracking()
            .Where(item => item.ShowcaseProjectId == showcaseId && item.CreatedAt >= startInstant && item.CreatedAt < endExclusiveInstant)
            .Select(item => item.CreatedAt).ToListAsync(cancellationToken);
        var commentsByDay = comments.GroupBy(value => DateOnly.FromDateTime(value)).ToDictionary(group => group.Key, group => group.Count());

        var daily = Enumerable.Range(0, 14).Select(offset =>
        {
            var date = start.AddDays(offset);
            return new ShowcaseInsightsDayDto { Date = date, Views = views.GetValueOrDefault(date), Upvotes = upvotesByDay.GetValueOrDefault(date), Comments = commentsByDay.GetValueOrDefault(date) };
        }).ToArray();
        int Before(IReadOnlyDictionary<DateOnly, int> source) => source.Where(item => item.Key < start).Sum(item => item.Value);
        var previous = new ShowcaseInsightsPeriodDto { Views = Before(views), Upvotes = Before(upvotesByDay), Comments = Before(commentsByDay) };
        return Result.Success(new ShowcaseInsightsDto { ViewCount = showcase.ViewCount, UpvoteCount = showcase.LikeCount, CommentCount = showcase.CommentCount, Daily = daily, Previous = previous });
    }

    private static Result<ShowcaseInsightsDto> NotFound() => Result.Failure<ShowcaseInsightsDto>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");
}
