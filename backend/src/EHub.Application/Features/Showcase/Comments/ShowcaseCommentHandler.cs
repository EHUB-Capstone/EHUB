using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Comments;

public sealed class ShowcaseCommentHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IDateTimeProvider time) : IShowcaseCommentHandler
{
    private const int MaximumPageSize = 50;
    private const int PreviewReplyCount = 3;

    public async Task<Result<PagedResponse<ShowcaseCommentDto>>> ListAsync(Guid showcaseId, int page, int pageSize, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthenticatedAsync(userId, cancellationToken)) return Unauthorized<PagedResponse<ShowcaseCommentDto>>();
        if (!await IsPublishedAsync(showcaseId, cancellationToken)) return NotFound<PagedResponse<ShowcaseCommentDto>>();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var query = context.ShowcaseComments.AsNoTracking().Where(item => item.ShowcaseProjectId == showcaseId && item.ParentCommentId == null);
        var total = await query.CountAsync(cancellationToken);
        var roots = await Rows(query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id))
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        // Only the replies of this page's comments are read, and only the oldest few of each: the rest load on demand.
        var rootIds = roots.Select(item => item.Id).ToList();
        var replyCounts = rootIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await context.ShowcaseComments.AsNoTracking()
                .Where(item => item.ParentCommentId != null && rootIds.Contains(item.ParentCommentId.Value))
                .GroupBy(item => item.ParentCommentId!.Value)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
        var previews = new Dictionary<Guid, List<CommentRow>>();
        foreach (var rootId in replyCounts.Keys)
            previews[rootId] = await ReplyRows(showcaseId, rootId).Take(PreviewReplyCount).ToListAsync(cancellationToken);

        var canModerate = await CanModerateAsync(showcaseId, userId, role, cancellationToken);
        var items = roots.Select(item => ToDto(item, userId, canModerate, replyCounts.GetValueOrDefault(item.Id),
            (previews.GetValueOrDefault(item.Id) ?? []).Select(reply => ToDto(reply, userId, canModerate, 0, [])).ToArray())).ToArray();
        return Result.Success(Paged(items, page, pageSize, total));
    }

    public async Task<Result<PagedResponse<ShowcaseCommentDto>>> ListRepliesAsync(Guid showcaseId, Guid commentId, int page, int pageSize, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthenticatedAsync(userId, cancellationToken)) return Unauthorized<PagedResponse<ShowcaseCommentDto>>();
        if (!await IsPublishedAsync(showcaseId, cancellationToken)) return NotFound<PagedResponse<ShowcaseCommentDto>>();
        var isThreadStart = await context.ShowcaseComments.AsNoTracking()
            .AnyAsync(item => item.Id == commentId && item.ShowcaseProjectId == showcaseId && item.ParentCommentId == null, cancellationToken);
        if (!isThreadStart) return CommentNotFound<PagedResponse<ShowcaseCommentDto>>();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var total = await context.ShowcaseComments.AsNoTracking().CountAsync(item => item.ParentCommentId == commentId, cancellationToken);
        var rows = await ReplyRows(showcaseId, commentId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var canModerate = await CanModerateAsync(showcaseId, userId, role, cancellationToken);
        var items = rows.Select(item => ToDto(item, userId, canModerate, 0, [])).ToArray();
        return Result.Success(Paged(items, page, pageSize, total));
    }

    public async Task<Result<ShowcaseCommentDto>> CreateAsync(Guid showcaseId, CreateShowcaseCommentRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthenticatedAsync(userId, cancellationToken)) return Unauthorized<ShowcaseCommentDto>();
        var content = request.Content.Trim();
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var showcase = await context.ShowcaseProjects.AsNoTracking()
                .Where(item => item.Id == showcaseId && item.Status == ShowcaseStatus.Published)
                .Select(item => new { item.ProjectLineageId })
                .FirstOrDefaultAsync(token);
            if (showcase is null) return NotFound<ShowcaseCommentDto>();
            // Replies stay one level deep: answering a reply joins the same thread as its parent.
            Guid? threadStartId = null;
            if (request.ParentCommentId is { } parentId)
            {
                var parent = await context.ShowcaseComments.AsNoTracking()
                    .Where(item => item.Id == parentId && item.ShowcaseProjectId == showcaseId)
                    .Select(item => new { item.Id, item.ParentCommentId })
                    .SingleOrDefaultAsync(token);
                if (parent is null) return CommentNotFound<ShowcaseCommentDto>();
                threadStartId = parent.ParentCommentId ?? parent.Id;
            }
            var project = await ShowcaseAccess.LatestProjectAsync(context, showcase.ProjectLineageId, token);
            if (project is null) return NotFound<ShowcaseCommentDto>();
            var now = time.UtcNow;
            var comment = new ShowcaseComment { ShowcaseProjectId = showcaseId, AuthorUserId = userId, ParentCommentId = threadStartId, Content = content, IsTeamReply = project.MemberUserIds.Contains(userId), CreatedAt = now, CreatedBy = userId };
            context.ShowcaseComments.Add(comment);
            ShowcaseOutbox.EnqueueNotification(context, showcaseId, project.MemberUserIds.Where(id => id != userId).ToArray(), "New comment on your showcase", $"A community member commented on “{project.ProjectName}”.", $"/showcase/{showcaseId}", now);
            await context.SaveChangesAsync(token);
            // Counted in SQL rather than on a tracked row: the page carries a row version, so two simultaneous comments would otherwise collide.
            await context.ShowcaseProjects.Where(item => item.Id == showcaseId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CommentCount, item => item.CommentCount + 1), token);
            var author = await context.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => new { user.FullName, user.AvatarUrl }).SingleAsync(token);
            return Result.Success(new ShowcaseCommentDto { Id = comment.Id, ParentCommentId = comment.ParentCommentId, Content = comment.Content, AuthorName = author.FullName, AuthorAvatarUrl = author.AvatarUrl, IsTeamReply = comment.IsTeamReply, CreatedAt = comment.CreatedAt, CanDelete = true });
        }, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid showcaseId, Guid commentId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthenticatedAsync(userId, cancellationToken)) return Result.Failure(ErrorCodes.CommonUnauthorizedError, "Authentication is required.");
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var comment = await context.ShowcaseComments.SingleOrDefaultAsync(item => item.Id == commentId && item.ShowcaseProjectId == showcaseId, token);
            if (comment is null) return Result.Failure(ErrorCodes.ShowcaseCommentNotFound, "The comment was not found.");
            var canModerate = ShowcaseAccess.IsAdmin(role);
            if (!canModerate && ShowcaseAccess.IsLecturer(role))
                canModerate = await ShowcaseAccess.ModeratableShowcases(context, userId, role)
                    .AnyAsync(item => item.Id == showcaseId, token);
            if (comment.AuthorUserId != userId && !canModerate) return Result.Failure(ErrorCodes.ShowcaseAccessDenied, "You cannot remove this comment.");
            // Removing a comment removes its replies too; otherwise they would stay hidden yet counted.
            var links = await context.ShowcaseComments.AsNoTracking()
                .Where(item => item.ShowcaseProjectId == showcaseId && item.ParentCommentId != null)
                .Select(item => new { item.Id, ParentId = item.ParentCommentId!.Value })
                .ToListAsync(token);
            var childrenByParent = links.ToLookup(link => link.ParentId, link => link.Id);
            var threadIds = new HashSet<Guid> { commentId };
            var pending = new Queue<Guid>([commentId]);
            while (pending.Count > 0)
                foreach (var childId in childrenByParent[pending.Dequeue()])
                    if (threadIds.Add(childId)) pending.Enqueue(childId);

            var now = time.UtcNow;
            var thread = await context.ShowcaseComments.Where(item => threadIds.Contains(item.Id)).ToListAsync(token);
            foreach (var item in thread) { item.IsDeleted = true; item.DeletedAt = now; item.DeletedBy = userId; }
            // Authors already know when they remove their own comment. A staff removal is communicated without
            // exposing the moderator or any report that may have prompted the decision.
            if (comment.AuthorUserId != userId)
                ShowcaseOutbox.EnqueueNotification(context, showcaseId, [comment.AuthorUserId],
                    "Comment hidden", "Bình luận của bạn đã bị ẩn vì vi phạm quy tắc cộng đồng.",
                    $"/showcase/{showcaseId}", now);
            var removed = thread.Count;
            await context.ShowcaseProjects.Where(item => item.Id == showcaseId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CommentCount, item => Math.Max(0, item.CommentCount - removed)), token);
            await context.SaveChangesAsync(token);
            return Result.Success();
        }, cancellationToken);
    }

    private async Task<bool> IsAuthenticatedAsync(Guid userId, CancellationToken token) =>
        userId != Guid.Empty && await context.Users.AsNoTracking().AnyAsync(user => user.Id == userId, token);
    private Task<bool> IsPublishedAsync(Guid showcaseId, CancellationToken token) => context.ShowcaseProjects.AsNoTracking().AnyAsync(item => item.Id == showcaseId && item.Status == ShowcaseStatus.Published, token);
    private static Result<T> Unauthorized<T>() => Result.Failure<T>(ErrorCodes.CommonUnauthorizedError, "Authentication is required.");
    private static Result<T> NotFound<T>() => Result.Failure<T>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");
    private static Result<T> CommentNotFound<T>() => Result.Failure<T>(ErrorCodes.ShowcaseCommentNotFound, "The comment was not found.");
    private async Task<bool> CanModerateAsync(Guid showcaseId, Guid userId, string role, CancellationToken token)
    {
        if (ShowcaseAccess.IsAdmin(role)) return true;
        return ShowcaseAccess.IsLecturer(role)
            && await ShowcaseAccess.ModeratableShowcases(context, userId, role).AnyAsync(item => item.Id == showcaseId, token);
    }

    private IQueryable<CommentRow> ReplyRows(Guid showcaseId, Guid threadStartId) =>
        Rows(context.ShowcaseComments.AsNoTracking()
            .Where(item => item.ShowcaseProjectId == showcaseId && item.ParentCommentId == threadStartId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id));

    private static IQueryable<CommentRow> Rows(IQueryable<ShowcaseComment> source) => source.Select(item => new CommentRow(
        item.Id, item.ParentCommentId, item.AuthorUserId, item.Content, item.AuthorUser.FullName, item.AuthorUser.AvatarUrl, item.IsTeamReply, item.CreatedAt));

    private static PagedResponse<ShowcaseCommentDto> Paged(IReadOnlyCollection<ShowcaseCommentDto> items, int page, int pageSize, int total) => new()
    {
        Items = items, PageIndex = page, PageSize = pageSize, TotalItems = total, TotalPages = (int)Math.Ceiling(total / (double)pageSize)
    };

    private sealed record CommentRow(Guid Id, Guid? ParentCommentId, Guid AuthorUserId, string Content, string AuthorName, string? AuthorAvatarUrl, bool IsTeamReply, DateTime CreatedAt);

    private static ShowcaseCommentDto ToDto(CommentRow item, Guid viewerId, bool canModerate, int replyCount, IReadOnlyCollection<ShowcaseCommentDto> replies) => new()
    {
        Id = item.Id, ParentCommentId = item.ParentCommentId, Content = item.Content, AuthorName = item.AuthorName,
        AuthorAvatarUrl = item.AuthorAvatarUrl, IsTeamReply = item.IsTeamReply, CreatedAt = item.CreatedAt,
        CanDelete = item.AuthorUserId == viewerId || canModerate, ReplyCount = replyCount, Replies = replies
    };
}
