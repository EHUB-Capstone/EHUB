using EHub.Shared.Results;

namespace EHub.Application.Features.Notifications.MarkNotificationRead;

public interface IMarkNotificationReadCommandHandler
{
    Task<Result> MarkReadAsync(
        Guid notificationId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task<Result> MarkAllReadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);
}
