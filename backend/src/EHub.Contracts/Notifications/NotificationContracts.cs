using System;
using System.Text.Json.Nodes;

namespace EHub.Contracts.Notifications;

public sealed class NotificationResponse
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Link { get; init; }
    public JsonNode? Data { get; init; }
    public string DataJson { get; init; } = "{}";
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class NotificationUnreadCountResponse
{
    public int Count { get; init; }
}
