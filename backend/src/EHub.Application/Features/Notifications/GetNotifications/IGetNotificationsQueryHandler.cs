using EHub.Contracts.Notifications;
using EHub.Shared.Results;

namespace EHub.Application.Features.Notifications.GetNotifications;

public interface IGetNotificationsQueryHandler
{
    Task<Result<IReadOnlyCollection<NotificationResponse>>> HandleAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task<Result<NotificationUnreadCountResponse>> GetUnreadCountAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);
}
