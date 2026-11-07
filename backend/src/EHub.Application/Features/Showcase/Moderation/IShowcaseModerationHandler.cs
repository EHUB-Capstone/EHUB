using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Moderation;

public enum ShowcaseModerationAction
{
    Approve,
    Reject,
    Hide,
    Unhide
}

public interface IShowcaseModerationHandler
{
    /// <summary>Owner asks for the page to be published; moves it to PendingReview.</summary>
    Task<Result<ShowcaseDto>> SubmitAsync(
        Guid showcaseId, SubmitShowcaseRequest request, Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>Owner takes the page down (or withdraws a pending request); moves it to Hidden.</summary>
    Task<Result<ShowcaseDto>> WithdrawAsync(
        Guid showcaseId, Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>Reviewing lecturer or admin decides on a showcase.</summary>
    Task<Result<ShowcaseDto>> DecideAsync(
        Guid showcaseId, ShowcaseModerationAction action, string? note, Guid userId, string role,
        CancellationToken cancellationToken = default);

    Task<Result<ShowcaseModerationPageDto>> ListAsync(
        string? status, int page, int pageSize, Guid userId, string role, CancellationToken cancellationToken = default);
}
