using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Comments;

public interface IShowcaseCommentHandler
{
    Task<Result<PagedResponse<ShowcaseCommentDto>>> ListAsync(Guid showcaseId, int page, int pageSize, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<PagedResponse<ShowcaseCommentDto>>> ListRepliesAsync(Guid showcaseId, Guid commentId, int page, int pageSize, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ShowcaseCommentDto>> CreateAsync(Guid showcaseId, CreateShowcaseCommentRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid showcaseId, Guid commentId, Guid userId, string role, CancellationToken cancellationToken = default);
}
