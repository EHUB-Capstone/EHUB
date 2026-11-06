using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Showcase;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Query;

public sealed class ShowcaseQueryHandler(IApplicationDbContext context) : IShowcaseQueryHandler
{
    public async Task<Result<ShowcaseDetailDto>> GetAsync(
        Guid showcaseId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var page = await context.ShowcaseProjects.AsNoTracking()
            .Where(item => item.Id == showcaseId)
            .Include(item => item.Links).Include(item => item.Media)
            .Select(item => new
            {
                Showcase = item,
                HasUpvoted = context.ShowcaseUpvotes.Any(vote => vote.ShowcaseProjectId == item.Id && vote.UserId == userId)
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (page is null) return NotFound();
        var showcase = page.Showcase;

        var isOwner = ShowcaseAccess.IsStudent(role) &&
                      await ShowcaseAccess.OwnsLineageAsync(context, showcase.ProjectLineageId, userId, cancellationToken);
        var canModerate = !isOwner && ShowcaseAccess.IsModerator(role) &&
                          await ShowcaseAccess.ModeratableShowcases(context, userId, role)
                              .AnyAsync(item => item.Id == showcaseId, cancellationToken);
        var isPublic = showcase.Status == ShowcaseStatus.Published;
        if (!isPublic && !isOwner && !canModerate) return NotFound();

        var privileged = isOwner || canModerate;
        var project = await ShowcaseAccess.LatestProjectAsync(context, showcase.ProjectLineageId, cancellationToken);

        // The team may choose not to list its members publicly; the team itself and reviewers always see them.
        var members = project is not null && (showcase.ShowTeamMembers || privileged)
            ? project.Members.Select(member => new ShowcaseTeamMemberDto
            {
                FullName = member.FullName,
                AvatarUrl = member.AvatarUrl,
                Role = member.Role
            }).ToArray()
            : [];

        IReadOnlyCollection<ShowcaseHistoryDto> history = [];
        if (privileged)
        {
            history = await context.ShowcaseReviewHistories.AsNoTracking()
                .Where(item => item.ShowcaseProjectId == showcaseId)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new ShowcaseHistoryDto
                {
                    FromStatus = item.FromStatus.ToString(),
                    ToStatus = item.ToStatus.ToString(),
                    Note = item.Note,
                    ActorName = item.Actor.FullName,
                    CreatedAt = item.CreatedAt
                })
                .ToArrayAsync(cancellationToken);
        }

        return Result.Success(new ShowcaseDetailDto
        {
            Showcase = ShowcaseMapper.ToDto(showcase, project?.Profile, canEdit: isOwner, includeReviewNote: privileged),
            TeamName = project?.TeamName ?? string.Empty,
            Members = members,
            History = history,
            IsOwner = isOwner,
            HasUpvoted = page.HasUpvoted,
            CanModerate = canModerate
        });
    }

    private static Result<ShowcaseDetailDto> NotFound() =>
        Result.Failure<ShowcaseDetailDto>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");
}
