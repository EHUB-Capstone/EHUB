using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Classes.Common;

internal static class ClassOutbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Enqueue(
        IApplicationDbContext context,
        string eventType,
        Guid classId,
        object data,
        DateTime? occurredAtUtc = null)
    {
        var eventId = Guid.NewGuid();
        var occurredAt = occurredAtUtc ?? DateTime.UtcNow;
        context.OutboxMessages.Add(new OutboxMessage
        {
            EventId = eventId,
            Type = eventType,
            AggregateType = "Class",
            AggregateId = classId,
            OccurredAtUtc = occurredAt,
            AvailableAtUtc = occurredAt,
            PayloadJson = JsonSerializer.Serialize(new
            {
                EventId = eventId,
                EventType = eventType,
                AggregateType = "Class",
                AggregateId = classId,
                OccurredAtUtc = occurredAt,
                Data = data
            }, JsonOptions)
        });
    }
}
