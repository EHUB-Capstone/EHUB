using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Showcase;

/// <summary>
/// Queues in-app notifications for showcase workflow changes. The outbox row is saved in the same SaveChanges as the
/// status change, and the dispatcher de-duplicates per event so retries cannot create duplicate notifications.
/// </summary>
internal static class ShowcaseOutbox
{
    public const string NotificationEventType = "Showcase.Notification.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void EnqueueNotification(
        IApplicationDbContext context,
        Guid showcaseId,
        IReadOnlyCollection<Guid> recipientUserIds,
        string title,
        string body,
        string link,
        DateTime occurredAtUtc,
        bool notifyAdministrators = false)
    {
        if (recipientUserIds.Count == 0 && !notifyAdministrators) return;

        var eventId = Guid.NewGuid();
        context.OutboxMessages.Add(new OutboxMessage
        {
            EventId = eventId,
            Type = NotificationEventType,
            AggregateType = "Showcase",
            AggregateId = showcaseId,
            OccurredAtUtc = occurredAtUtc,
            AvailableAtUtc = occurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(new
            {
                EventId = eventId,
                EventType = NotificationEventType,
                AggregateType = "Showcase",
                AggregateId = showcaseId,
                OccurredAtUtc = occurredAtUtc,
                Data = new
                {
                    RecipientUserIds = recipientUserIds,
                    NotifyAdministrators = notifyAdministrators,
                    Title = title,
                    Body = body,
                    Link = link
                }
            }, JsonOptions)
        });
    }
}
