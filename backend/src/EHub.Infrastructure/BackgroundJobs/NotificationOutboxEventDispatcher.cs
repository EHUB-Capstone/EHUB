using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class NotificationOutboxEventDispatcher : IOutboxEventDispatcher
{
    private readonly AppDbContext _context;
    private readonly IClassChatMembershipSynchronizer _chatMembershipSynchronizer;
    private readonly ILogger<NotificationOutboxEventDispatcher> _logger;

    public NotificationOutboxEventDispatcher(
        AppDbContext context,
        IClassChatMembershipSynchronizer chatMembershipSynchronizer,
        ILogger<NotificationOutboxEventDispatcher> logger)
    {
        _context = context;
        _chatMembershipSynchronizer = chatMembershipSynchronizer;
        _logger = logger;
    }

    public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(message.PayloadJson);
        if (!document.RootElement.TryGetProperty("data", out var data))
        {
            _logger.LogInformation("Outbox event {OutboxEventId} has no notification payload", message.EventId);
            return;
        }

        switch (message.Type)
        {
            case "TeamProposal.Submitted.v1":
                await AddForOptionalUserAsync(message, data, "lecturerUserId", NotificationType.ProposalSubmitted,
                    "Team proposal awaiting review", "A student team proposal is ready for your review.", cancellationToken);
                break;
            case "TeamProposal.Reviewed.v1":
                var proposalDecision = ReadString(data, "decision");
                await AddForOptionalUserAsync(message, data, "studentUserId",
                    proposalDecision == "Approved" ? NotificationType.ProposalApproved : NotificationType.ProposalNeedsRevision,
                    "Team proposal reviewed", $"Your team proposal was reviewed: {proposalDecision}.", cancellationToken);
                break;
            case "ProjectDirection.Submitted.v1":
                await AddForOptionalUserAsync(message, data, "lecturerUserId", NotificationType.ProjectDirectionSubmitted,
                    "Project direction awaiting review", "A team submitted its project direction for your review.", cancellationToken);
                break;
            case "ProjectDirection.Reviewed.v1":
                var directionDecision = ReadString(data, "decision");
                if (data.TryGetProperty("studentUserIds", out var recipients) && recipients.ValueKind == JsonValueKind.Array)
                {
                    foreach (var recipient in recipients.EnumerateArray())
                    {
                        if (recipient.TryGetGuid(out var userId))
                            await AddAsync(message, userId,
                                directionDecision == "Approved" ? NotificationType.ProjectDirectionApproved : NotificationType.ProjectDirectionNeedsRevision,
                                "Project direction reviewed", $"Your project direction was reviewed: {directionDecision}.", cancellationToken);
                    }
                }
                break;
            case "Team.MentorAssignmentChanged.v1" when ReadString(data, "action") is "Assigned" or "Reassigned":
                await AddForOptionalUserAsync(message, data, "mentorUserId", NotificationType.MentorAssigned,
                    "Mentor assignment", "You have been assigned to mentor a team.", cancellationToken);
                break;
            default:
                _logger.LogDebug("Outbox event {OutboxEventId} {OutboxEventType} has no notification projection", message.EventId, message.Type);
                break;
        }

        if (RequiresChatSynchronization(message.Type))
            await _chatMembershipSynchronizer.SynchronizeAsync(message.AggregateId, cancellationToken: cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static bool RequiresChatSynchronization(string eventType) => eventType is
        "Class.Created.v1" or
        "Class.TeachingAssignmentChanged.v1" or
        "Class.StudentEnrollmentAdded.v1" or
        "Class.StudentEnrollmentDropped.v1" or
        "Class.StudentReEnrolled.v1" or
        "Class.StudentRosterImported.v1" or
        "Team.Created.v1" or
        "Team.MembersUpdated.v1" or
        "Team.LeaderAssigned.v1" or
        "Team.MentorAssignmentChanged.v1" or
        "Class.Archived.v1" or
        "Class.Restored.v1";

    private async Task AddForOptionalUserAsync(
        OutboxMessage message,
        JsonElement data,
        string propertyName,
        NotificationType type,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        if (data.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null && property.TryGetGuid(out var userId))
            await AddAsync(message, userId, type, title, body, cancellationToken);
    }

    private async Task AddAsync(
        OutboxMessage message,
        Guid recipientUserId,
        NotificationType type,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        if (await _context.Notifications.IgnoreQueryFilters().AnyAsync(notification =>
                notification.SourceEventId == message.EventId && notification.RecipientUserId == recipientUserId, cancellationToken))
            return;

        _context.Notifications.Add(new Notification
        {
            SourceEventId = message.EventId,
            RecipientUserId = recipientUserId,
            Type = type,
            Title = title,
            Body = body,
            Link = BuildLink(message),
            DataJson = message.PayloadJson,
            CreatedAt = message.OccurredAtUtc
        });
    }

    private static string? BuildLink(OutboxMessage message) => message.Type switch
    {
        "TeamProposal.Submitted.v1" or "ProjectDirection.Submitted.v1" => $"/classes/{message.AggregateId}",
        "TeamProposal.Reviewed.v1" or "ProjectDirection.Reviewed.v1" => $"/student/classes/{message.AggregateId}",
        "Team.MentorAssignmentChanged.v1" => "/mentor/dashboard",
        _ => null
    };

    private static string ReadString(JsonElement data, string propertyName) =>
        data.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
