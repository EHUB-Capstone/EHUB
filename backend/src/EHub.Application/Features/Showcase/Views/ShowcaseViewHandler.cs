using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Showcase;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Views;

public sealed class ShowcaseViewHandler(
    IApplicationDbContext context,
    IShowcaseViewStore store,
    IDateTimeProvider time) : IShowcaseViewHandler
{
    public async Task<Result<ShowcaseViewDto>> RecordAsync(
        Guid showcaseId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !await context.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
            return Result.Failure<ShowcaseViewDto>(ErrorCodes.CommonUnauthorizedError, "Authentication is required.");

        if (ShowcaseAccess.IsStudent(role))
        {
            var showcase = await context.ShowcaseProjects.AsNoTracking()
                .Where(page => page.Id == showcaseId && page.Status == ShowcaseStatus.Published)
                .Select(page => new { page.ProjectLineageId, page.ViewCount })
                .SingleOrDefaultAsync(cancellationToken);
            if (showcase is null)
                return NotFound();

            if (await ShowcaseAccess.OwnsLineageAsync(context, showcase.ProjectLineageId, userId, cancellationToken))
                return Result.Success(new ShowcaseViewDto { ViewCount = showcase.ViewCount });
        }

        var viewDateUtc = DateOnly.FromDateTime(time.UtcNow);
        var viewCount = await store.RecordAsync(showcaseId, userId, viewDateUtc, cancellationToken);
        return viewCount.HasValue
            ? Result.Success(new ShowcaseViewDto { ViewCount = viewCount.Value })
            : NotFound();
    }

    private static Result<ShowcaseViewDto> NotFound() =>
        Result.Failure<ShowcaseViewDto>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");
}
