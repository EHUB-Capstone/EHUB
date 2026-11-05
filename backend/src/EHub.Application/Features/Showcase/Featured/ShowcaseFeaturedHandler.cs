using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Featured;

public sealed class ShowcaseFeaturedHandler(IApplicationDbContext context, IDateTimeProvider time) : IShowcaseFeaturedHandler
{
    public async Task<Result<ShowcaseFeaturedDto>> SetAsync(
        Guid showcaseId, bool featured, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsModerator(role))
            return Result.Failure<ShowcaseFeaturedDto>(ErrorCodes.ShowcaseAccessDenied, "Only lecturers and administrators can select featured showcases.");
        // Resolve review scope before inspecting the state, so foreign ids always look missing.
        var showcase = await ShowcaseAccess.ModeratableShowcases(context, userId, role)
            .FirstOrDefaultAsync(item => item.Id == showcaseId, cancellationToken);
        if (showcase is null)
            return Result.Failure<ShowcaseFeaturedDto>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");
        if (showcase.Status != ShowcaseStatus.Published)
            return Result.Failure<ShowcaseFeaturedDto>(ErrorCodes.ShowcaseInvalidState, "Only published showcases can be featured or have their featured flag removed.");
        // Retried requests do not change the original timestamp or create duplicate history.
        if (showcase.IsFeatured == featured) return Result.Success(ToDto(showcase));

        var now = time.UtcNow;
        // PostgreSQL stores microseconds. Match that precision before saving so the first response
        // and an idempotent retry report exactly the same stored selection timestamp.
        now = now.AddTicks(-(now.Ticks % 10));
        showcase.IsFeatured = featured;
        showcase.FeaturedAt = featured ? now : null;
        showcase.UpdatedBy = userId;
        context.ShowcaseReviewHistories.Add(new ShowcaseReviewHistory
        {
            ShowcaseProjectId = showcase.Id,
            FromStatus = showcase.Status, ToStatus = showcase.Status,
            Note = featured ? "Selected as a featured showcase by faculty." : "Featured selection removed by faculty.",
            ActorId = userId, CreatedAt = now
        });
        try
        {
            // Flag and audit history are atomic; existing xmin handles concurrent edits/hides.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ClearChanges();
            return Result.Failure<ShowcaseFeaturedDto>(ErrorCodes.ShowcaseConcurrencyConflict, "The showcase page was changed by someone else. Reload and try again.");
        }
        return Result.Success(ToDto(showcase));
    }

    private static ShowcaseFeaturedDto ToDto(ShowcaseProject showcase) => new()
    {
        Id = showcase.Id, IsFeatured = showcase.IsFeatured, FeaturedAt = showcase.FeaturedAt, RowVersion = showcase.Version
    };
}
