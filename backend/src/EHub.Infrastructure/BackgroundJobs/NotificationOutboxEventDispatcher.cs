using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class NotificationOutboxEventDispatcher : IOutboxEventDispatcher
{
    private readonly AppDbContext _context;
    private readonly IClassChatMembershipSynchronizer _chatMembershipSynchronizer;
    private readonly IProjectDirectionRealtimePublisher _projectDirectionRealtimePublisher;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationOutboxEventDispatcher> _logger;

    public NotificationOutboxEventDispatcher(
        AppDbContext context,
        IClassChatMembershipSynchronizer chatMembershipSynchronizer,
        IProjectDirectionRealtimePublisher projectDirectionRealtimePublisher,
        IEmailService emailService,
        ILogger<NotificationOutboxEventDispatcher> logger)
    {
        _context = context;
        _chatMembershipSynchronizer = chatMembershipSynchronizer;
        _projectDirectionRealtimePublisher = projectDirectionRealtimePublisher;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Guid> realtimeNotificationRecipients = [];
        Guid? realtimeNotificationTeamId = null;
        using var document = JsonDocument.Parse(message.PayloadJson);
        if (!document.RootElement.TryGetProperty("data", out var data))
        {
            _logger.LogInformation("Outbox event {OutboxEventId} has no notification payload", message.EventId);
            return;
        }

        switch (message.Type)
        {
            case "Class.Created.v1":
                var createdClassDetails = await GetClassEmailDetailsAsync(message.AggregateId, cancellationToken);
                await AddForOptionalUserAsync(
                    message, data, "primaryLecturerId", NotificationType.SystemAnnouncement,
                    "You have been assigned to a class",
                    $"You have been assigned as the lecturer for class {createdClassDetails.ClassCode}.", cancellationToken);
                await SendClassCreatedEmailAsync(data, createdClassDetails, cancellationToken);
                break;
            case "Class.StudentRosterImported.v1":
                var importedClassDetails = await GetClassEmailDetailsAsync(message.AggregateId, cancellationToken);
                await AddForUsersAsync(
                    message, data, "studentUserIds", NotificationType.SystemAnnouncement,
                    "You have been added to a class",
                    $"You have been added to class {importedClassDetails.ClassCode}.", cancellationToken);
                await SendStudentImportEmailsAsync(data, importedClassDetails, cancellationToken);
                break;
            case "TeamProposal.Submitted.v1":
                if (ReadBoolean(data, "adminReviewRequired"))
                {
                    await AddForAdministratorsAsync(
                        message,
                        NotificationType.ProposalSubmitted,
                        "Exception team proposal awaiting review",
                        "A 3- or 7-member team proposal requires administrator review.",
                        cancellationToken);
                }
                else
                {
                    await AddForOptionalUserAsync(
                        message,
                        data,
                        "lecturerUserId",
                        NotificationType.ProposalSubmitted,
                        "Team proposal awaiting review",
                        "A student team proposal is ready for your review.",
                        cancellationToken);
                }
                break;
            case "TeamProposal.Reviewed.v1":
                var proposalDecision = ReadString(data, "decision");
                var notificationType = proposalDecision == "Approved"
                    ? NotificationType.ProposalApproved
                    : NotificationType.ProposalNeedsRevision;
                var notificationTitle = proposalDecision == "Rejected"
                    ? "Team proposal rejected"
                    : "Team proposal reviewed";
                var notificationBody = proposalDecision == "Rejected"
                    ? "Your team proposal was rejected. Read the review comment for details."
                    : $"Your team proposal was reviewed: {proposalDecision}.";
                if (proposalDecision == "NeedsRevision")
                {
                    await AddForOptionalUserAsync(
                        message,
                        data,
                        "teamLeaderUserId",
                        notificationType,
                        notificationTitle,
                        notificationBody,
                        cancellationToken);
                }
                else
                {
                    await AddForUsersAsync(
                        message,
                        data,
                        "studentUserIds",
                        notificationType,
                        notificationTitle,
                        notificationBody,
                        cancellationToken);
                }
                break;
            case "Team.MembersUpdated.v1":
                await AddForUsersAsync(
                    message,
                    data,
                    "memberUserIds",
                    NotificationType.SystemAnnouncement,
                    "Team membership updated",
                    "Your team membership or leader assignment was updated.",
                    cancellationToken);
                break;
            case "Team.LeaderAssigned.v1":
                await AddForUsersAsync(
                    message,
                    data,
                    "memberUserIds",
                    NotificationType.SystemAnnouncement,
                    "Team leader updated",
                    "Your team leader has been updated.",
                    cancellationToken);
                break;
            case "Team.Archived.v1":
                await AddForUsersAsync(
                    message,
                    data,
                    "memberUserIds",
                    NotificationType.SystemAnnouncement,
                    "Team archived",
                    "Your team was archived and you are no longer assigned to it.",
                    cancellationToken);
                break;
            case "ProjectDirection.Submitted.v1":
                await AddForOptionalUserAsync(message, data, "lecturerUserId", NotificationType.ProjectDirectionSubmitted,
                    "Project direction awaiting review", "A team submitted its project direction for your review.", cancellationToken);
                var lecturerUserId = ReadGuid(data, "lecturerUserId");
                if (lecturerUserId.HasValue) realtimeNotificationRecipients = [lecturerUserId.Value];
                realtimeNotificationTeamId = ReadGuid(data, "teamId");
                break;
            case "ProjectDirection.Reviewed.v1":
                var directionDecision = ReadString(data, "decision");
                realtimeNotificationRecipients = ReadGuids(data, "studentUserIds");
                realtimeNotificationTeamId = ReadGuid(data, "teamId");
                foreach (var userId in realtimeNotificationRecipients)
                {
                    await AddAsync(message, userId,
                        directionDecision == "Approved" ? NotificationType.ProjectDirectionApproved : NotificationType.ProjectDirectionNeedsRevision,
                        "Project direction reviewed", $"Your project direction was reviewed: {directionDecision}.", cancellationToken);
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
        if (realtimeNotificationTeamId.HasValue && realtimeNotificationRecipients.Count > 0)
            await _projectDirectionRealtimePublisher.PublishNotificationReadyAsync(
                realtimeNotificationRecipients,
                message.AggregateId,
                realtimeNotificationTeamId.Value,
                cancellationToken);
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
        "Team.Archived.v1" or
        "TeamProposal.Reviewed.v1" or
        "Class.Archived.v1" or
        "Class.Restored.v1" or
        "Class.Completed.v1" or
        "Class.Reopened.v1";

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

    private async Task SendClassCreatedEmailAsync(
        JsonElement data,
        ClassEmailDetails classDetails,
        CancellationToken cancellationToken)
    {
        var lecturerUserId = ReadGuid(data, "primaryLecturerId");
        if (!lecturerUserId.HasValue) return;

        var lecturer = await _context.Users.AsNoTracking()
            .Where(user => user.Id == lecturerUserId.Value && user.Status == UserStatus.Active)
            .Select(user => new { user.Email, user.FullName })
            .FirstOrDefaultAsync(cancellationToken);
        if (lecturer is null || string.IsNullOrWhiteSpace(lecturer.Email)) return;

        await _emailService.SendClassNotificationAsync(
            lecturer.Email, lecturer.FullName,
            $"[E-HUB] Teaching Assignment: {classDetails.ClassCode}",
            $"Teaching Assignment: {classDetails.ClassCode}",
            $"""
            Dear {lecturer.FullName},

            E-HUB is pleased to inform you that you have been assigned as the lecturer for class {classDetails.ClassCode} in {classDetails.Semester}.

            Class details:
            • Course: {classDetails.SubjectName}
            • Class code: {classDetails.ClassCode}
        

            Please sign in to E-HUB to view the student roster, manage the class, and carry out course activities.

            Kind regards,
            E-HUB – Entrepreneurship Hub
            """, cancellationToken);
    }

    private async Task SendStudentImportEmailsAsync(
        JsonElement data,
        ClassEmailDetails classDetails,
        CancellationToken cancellationToken)
    {
        if (!data.TryGetProperty("studentRecipients", out var recipients) || recipients.ValueKind != JsonValueKind.Array) return;

        foreach (var recipient in recipients.EnumerateArray())
        {
            var email = ReadString(recipient, "email");
            if (string.IsNullOrWhiteSpace(email)) continue;

            await _emailService.SendClassNotificationAsync(
                email, ReadString(recipient, "fullName"),
                $"[E-HUB] You have been added to {classDetails.ClassCode}",
                $"You have been added to {classDetails.ClassCode}",
                $"""
                Dear {ReadString(recipient, "fullName")},

                E-HUB is pleased to inform you that you have been added to class {classDetails.ClassCode} in {classDetails.Semester}.

                Class details:
                • Course: {classDetails.SubjectName}
                • Class code: {classDetails.ClassCode}
                • Lecturer: {classDetails.LecturerName}
                • Room: {classDetails.Room}
                • Schedule: {classDetails.Schedule}

                Please sign in to E-HUB to review the class information, follow announcements, and participate in course activities when available.

                If this assignment is not correct, please contact the assigned lecturer or an E-HUB administrator.

                Kind regards,
                E-HUB – Entrepreneurship Hub
                """, cancellationToken);
        }
    }

    private async Task<ClassEmailDetails> GetClassEmailDetailsAsync(Guid classId, CancellationToken cancellationToken)
    {
        var @class = await _context.Classes.AsNoTracking()
            .Include(item => item.Course)
            .Include(item => item.Semester)
            .Include(item => item.PrimaryLecturer)
            .FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (@class is null)
            return new ClassEmailDetails("your class", "Not available", "Not available", "Not available", "Not available", "Not available");

        return new ClassEmailDetails(
            @class.ClassCode,
            @class.Course.Name,
            $"{@class.Semester.Code} – {@class.Semester.Year}",
            string.IsNullOrWhiteSpace(@class.Room) ? "Not available" : @class.Room,
            FormatSchedule(@class.ScheduleJson),
            @class.PrimaryLecturer?.FullName ?? "Not available");
    }

    private static string FormatSchedule(string? scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson)) return "Not available";
        try
        {
            using var document = JsonDocument.Parse(scheduleJson);
            var slots = document.RootElement.EnumerateArray()
                .Select(slot =>
                {
                    var day = slot.TryGetProperty("dayOfWeek", out var dayValue) && dayValue.TryGetInt32(out var dayNumber)
                        ? ToEnglishDay(dayNumber)
                        : "";
                    var slotNumber = slot.TryGetProperty("slotNumber", out var slotValue) && slotValue.TryGetInt32(out var parsedSlot)
                        ? $"Tiết {parsedSlot}"
                        : "";
                    var room = ReadString(slot, "room");
                    return string.Join(" · ", new[] { day, slotNumber, room }.Where(value => !string.IsNullOrWhiteSpace(value)));
                })
                .Where(value => !string.IsNullOrWhiteSpace(value));
            return string.Join("; ", slots) is { Length: > 0 } value ? value : "Not available";
        }
        catch (JsonException)
        {
            return "Not available";
        }
    }

    private static string ToEnglishDay(int dayOfWeek) => dayOfWeek switch
    {
        1 => "Monday",
        2 => "Tuesday",
        3 => "Wednesday",
        4 => "Thursday",
        5 => "Friday",
        6 => "Saturday",
        _ => string.Empty
    };

    private sealed record ClassEmailDetails(
        string ClassCode,
        string SubjectName,
        string Semester,
        string Room,
        string Schedule,
        string LecturerName);

    private async Task<string> GetClassCodeAsync(Guid classId, CancellationToken cancellationToken) =>
        await _context.Classes.AsNoTracking()
            .Where(@class => @class.Id == classId)
            .Select(@class => @class.ClassCode)
            .FirstOrDefaultAsync(cancellationToken) ?? "your class";

    private async Task AddForUsersAsync(
        OutboxMessage message,
        JsonElement data,
        string propertyName,
        NotificationType type,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        if (!data.TryGetProperty(propertyName, out var recipients) || recipients.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var recipient in recipients.EnumerateArray())
        {
            if (recipient.TryGetGuid(out var userId))
            {
                await AddAsync(message, userId, type, title, body, cancellationToken);
            }
        }
    }

    private async Task AddForAdministratorsAsync(
        OutboxMessage message,
        NotificationType type,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        var administratorIds = await _context.Users
            .AsNoTracking()
            .Where(user => user.Status == UserStatus.Active && user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin))
            .Select(user => user.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var administratorId in administratorIds)
        {
            await AddAsync(message, administratorId, type, title, body, cancellationToken);
        }
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

        var link = await BuildLinkAsync(message, cancellationToken);
        _context.Notifications.Add(new Notification
        {
            SourceEventId = message.EventId,
            RecipientUserId = recipientUserId,
            Type = type,
            Title = title,
            Body = body,
            Link = link,
            DataJson = message.PayloadJson,
            CreatedAt = message.OccurredAtUtc
        });
    }

    private async Task<string?> BuildLinkAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (message.Type == "ProjectDirection.Submitted.v1")
        {
            var classPeriod = await _context.Classes
                .AsNoTracking()
                .Where(item => item.Id == message.AggregateId)
                .Select(item => new { item.Semester.Code, item.Semester.Year })
                .FirstOrDefaultAsync(cancellationToken);
            var teamId = ReadPayloadGuid(message.PayloadJson, "teamId");
            var query = new List<string>();

            if (classPeriod != null)
            {
                var semester = classPeriod.Code.Length >= 2
                    ? classPeriod.Code[..2].ToUpperInvariant()
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(semester)) query.Add($"semester={Uri.EscapeDataString(semester)}");
                query.Add($"year={classPeriod.Year}");
            }

            query.Add("tab=overview");
            query.Add($"classId={message.AggregateId}");
            if (teamId.HasValue) query.Add($"teamId={teamId.Value}");
            return $"/lecturer/classes?{string.Join('&', query)}";
        }

        return message.Type switch
        {
            "TeamProposal.Submitted.v1" => $"/classes/{message.AggregateId}",
            "TeamProposal.Reviewed.v1" or "ProjectDirection.Reviewed.v1" => $"/student/classes/{message.AggregateId}",
            "Team.MentorAssignmentChanged.v1" => "/mentor/dashboard",
            _ => null
        };
    }

    private static Guid? ReadPayloadGuid(string payloadJson, string propertyName)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty(propertyName, out var property)
            && property.TryGetGuid(out var value)
                ? value
                : null;
    }

    private static string ReadString(JsonElement data, string propertyName) =>
        data.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static Guid? ReadGuid(JsonElement data, string propertyName) =>
        data.TryGetProperty(propertyName, out var value) && value.TryGetGuid(out var parsed)
            ? parsed
            : null;

    private static Guid[] ReadGuids(JsonElement data, string propertyName) =>
        data.TryGetProperty(propertyName, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray()
                .Select(value => value.TryGetGuid(out var parsed) ? (Guid?)parsed : null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .Distinct()
                .ToArray()
            : [];

    private static bool ReadBoolean(JsonElement data, string propertyName) =>
        data.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
}
