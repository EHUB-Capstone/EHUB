using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Showcase;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Upvote;

public sealed class ShowcaseUpvoteHandler(
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    IShowcaseUpvoteStore store,
    IDateTimeProvider time) : IShowcaseUpvoteHandler
{
    public async Task<Result<ShowcaseUpvoteDto>> SetAsync(
        Guid showcaseId, Guid userId, string role, bool upvoted, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !await context.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
            return Result.Failure<ShowcaseUpvoteDto>(ErrorCodes.CommonUnauthorizedError, "Authentication is required.");

        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var showcase = await store.LockPublishedAsync(showcaseId, token);
            if (showcase is null)
                return Result.Failure<ShowcaseUpvoteDto>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");

            if (upvoted && ShowcaseAccess.IsStudent(role) &&
                await ShowcaseAccess.OwnsLineageAsync(context, showcase.ProjectLineageId, userId, token))
                return Result.Failure<ShowcaseUpvoteDto>(ErrorCodes.ShowcaseAccessDenied, "You cannot upvote your own showcase.");

            var changed = upvoted
                ? await store.TryAddAsync(showcaseId, userId, time.UtcNow, token)
                : await context.ShowcaseUpvotes
                    .Where(vote => vote.ShowcaseProjectId == showcaseId && vote.UserId == userId)
                    .ExecuteDeleteAsync(token) == 1;

            var likeCount = showcase.LikeCount;
            if (changed)
            {
                var delta = upvoted ? 1 : -1;
                await context.ShowcaseProjects.Where(item => item.Id == showcaseId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LikeCount, item => item.LikeCount + delta), token);
                likeCount += delta;
            }

            return Result.Success(new ShowcaseUpvoteDto
            {
                LikeCount = likeCount,
                HasUpvoted = upvoted
            });
        }, cancellationToken);
    }
}
