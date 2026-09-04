using EHub.Application.Common.Interfaces.Persistence;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Notifications.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler(IApplicationDbContext context) : IMarkNotificationReadCommandHandler
{
    public async Task<Result> MarkReadAsync(
        Guid notificationId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        if (recipientUserId == Guid.Empty)
        {
            return Result.Failure(
                ErrorCodes.CommonUnauthorizedError,
                "A signed-in user is required.");
        }

        var notification = await context.Notifications
            .FirstOrDefaultAsync(item =>
                item.Id == notificationId &&
                item.RecipientUserId == recipientUserId,
                cancellationToken);

        if (notification is null)
        {
            return Result.Failure(
                ErrorCodes.NotificationNotFound,
                "Notification was not found.");
        }

        if (notification.IsRead)
        {
            return Result.Success();
        }

        var now = DateTime.UtcNow;
        notification.IsRead = true;
        notification.ReadAt = now;
        notification.UpdatedBy = recipientUserId;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        if (recipientUserId == Guid.Empty)
        {
            return Result.Failure(
                ErrorCodes.CommonUnauthorizedError,
                "A signed-in user is required.");
        }

        var notifications = await context.Notifications
            .Where(notification =>
                notification.RecipientUserId == recipientUserId &&
                !notification.IsRead)
            .ToArrayAsync(cancellationToken);

        if (notifications.Length == 0)
        {
            return Result.Success();
        }

        var now = DateTime.UtcNow;
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
            notification.UpdatedBy = recipientUserId;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
