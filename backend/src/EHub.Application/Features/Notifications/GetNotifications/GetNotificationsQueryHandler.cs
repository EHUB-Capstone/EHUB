using System.Text.Json;
using System.Text.Json.Nodes;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Notifications;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Notifications.GetNotifications;

public sealed class GetNotificationsQueryHandler(IApplicationDbContext context) : IGetNotificationsQueryHandler
{
    private const int MaxNotificationCount = 50;

    public async Task<Result<IReadOnlyCollection<NotificationResponse>>> HandleAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        if (recipientUserId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyCollection<NotificationResponse>>(
                ErrorCodes.CommonUnauthorizedError,
                "A signed-in user is required.");
        }

        var notifications = await context.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == recipientUserId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(MaxNotificationCount)
            .Select(notification => new NotificationProjection
            {
                Id = notification.Id,
                Type = notification.Type.ToString(),
                Title = notification.Title,
                Message = notification.Body,
                Link = notification.Link,
                DataJson = notification.DataJson,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                CreatedAt = notification.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        var response = notifications
            .Select(ToResponse)
            .ToArray();

        return Result.Success<IReadOnlyCollection<NotificationResponse>>(response);
    }

    public async Task<Result<NotificationUnreadCountResponse>> GetUnreadCountAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        if (recipientUserId == Guid.Empty)
        {
            return Result.Failure<NotificationUnreadCountResponse>(
                ErrorCodes.CommonUnauthorizedError,
                "A signed-in user is required.");
        }

        var count = await context.Notifications
            .AsNoTracking()
            .CountAsync(notification =>
                notification.RecipientUserId == recipientUserId &&
                !notification.IsRead,
                cancellationToken);

        return Result.Success(new NotificationUnreadCountResponse
        {
            Count = count
        });
    }

    private static NotificationResponse ToResponse(NotificationProjection notification)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Link = notification.Link,
            Data = ParseData(notification.DataJson),
            DataJson = notification.DataJson,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };
    }

    private static JsonNode? ParseData(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            var root = JsonNode.Parse(dataJson);
            return root?["data"]?.DeepClone() ?? root;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class NotificationProjection
    {
        public Guid Id { get; init; }
        public string Type { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string? Link { get; init; }
        public string DataJson { get; init; } = "{}";
        public bool IsRead { get; init; }
        public DateTime? ReadAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
