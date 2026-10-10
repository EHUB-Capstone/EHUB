using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Models.Identity;
using EHub.Application.Features.Checkpoints.LecturerManagement;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class NotificationOutboxEventDispatcher : IOutboxEventDispatcher
{
    internal const string ClassEmailEventType = "Class.NotificationEmailRequested.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _context;
    private readonly IClassChatMembershipSynchronizer _chatMembershipSynchronizer;
    private readonly IProjectDirectionRealtimePublisher _projectDirectionRealtimePublisher;
    private readonly IClassRealtimePublisher _classRealtimePublisher;
    private readonly IEmailService _emailService;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<NotificationOutboxEventDispatcher> _logger;

    public NotificationOutboxEventDispatcher(
        AppDbContext context,
        IClassChatMembershipSynchronizer chatMembershipSynchronizer,
        IProjectDirectionRealtimePublisher projectDirectionRealtimePublisher,
        IClassRealtimePublisher classRealtimePublisher,
        IEmailService emailService,
        ILogger<NotificationOutboxEventDispatcher> logger,
        IOptions<FrontendOptions>? frontendOptions = null)
    {
        _context = context;
        _chatMembershipSynchronizer = chatMembershipSynchronizer;
        _projectDirectionRealtimePublisher = projectDirectionRealtimePublisher;
        _classRealtimePublisher = classRealtimePublisher;
        _emailService = emailService;
        _frontendOptions = frontendOptions?.Value ?? new FrontendOptions();
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
            case "AccountApproval.Requested.v1":
                var accountRole = ReadString(data, "role");
                var applicantName = ReadString(data, "fullName");
                await AddForAdministratorsAsync(
                    message,
                    NotificationType.AccountApprovalRequested,
                    $"{accountRole} account awaiting approval",
                    $"{applicantName} registered as a {accountRole} and is ready for review.",
                    cancellationToken);
                break;
            case "Class.Created.v1":
                var createdClassDetails = await GetClassEmailDetailsAsync(message.AggregateId, cancellationToken);
                await AddForOptionalUserAsync(
                    message, data, "primaryLecturerId", NotificationType.SystemAnnouncement,
                    "You have been assigned to a class",
                    $"You have been assigned as the lecturer for class {createdClassDetails.ClassCode}.", cancellationToken);
                await QueueClassCreatedEmailAsync(message, data, createdClassDetails, cancellationToken);
                break;
            case "Class.StudentRosterImported.v1":
                var importedClassDetails = await GetClassEmailDetailsAsync(message.AggregateId, cancellationToken);
                await AddForUsersAsync(
                    message, data, "studentUserIds", NotificationType.SystemAnnouncement,
                    "You have been added to a class",
                    $"You have been added to class {importedClassDetails.ClassCode}.", cancellationToken);
                await QueueStudentImportEmailsAsync(message, data, importedClassDetails, cancellationToken);
                break;
            case "Class.EnrollmentMajorsAutoLocked.v1":
                var lockedClassCode = await GetClassCodeAsync(message.AggregateId, cancellationToken);
                await AddForUsersAsync(
                    message, data, "studentUserIds", NotificationType.SystemAnnouncement,
                    "Major verification completed",
                    $"Majors for class {lockedClassCode} have been verified and major updates are now locked. Contact your lecturer if your major needs to be corrected.",
                    cancellationToken);
                break;
            case CheckpointDeadlineEvents.ScheduleChanged:
                await AddCheckpointDeadlineNotificationsAsync(message, data, false, cancellationToken);
                break;
            case CheckpointDeadlineEvents.DeadlineReminder:
                await AddCheckpointDeadlineNotificationsAsync(message, data, true, cancellationToken);
                break;
            case CheckpointDeadlineEvents.DeadlineOverdue:
                await AddCheckpointOverdueNotificationsAsync(message, data, cancellationToken);
                break;
            case CheckpointDeadlineEvents.DeadlineExtensionRequested:
                await AddDeadlineExtensionRequestNotificationsAsync(message, data, cancellationToken);
                break;
            case ClassEmailEventType:
                await _emailService.SendClassNotificationAsync(
                    ReadString(data, "email"), ReadString(data, "fullName"),
                    ReadString(data, "subject"), ReadString(data, "title"),
                    ReadString(data, "body"), cancellationToken);
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
            case "TeamFormation.Invited.v1":
                var invitedTeamName = ReadString(data, "teamName");
                var proposedLeaderId = ReadGuid(data, "proposedLeaderStudentId");
                var invitedStudentIds = ReadGuids(data, "studentIds");
                var invitedLeaderIds = proposedLeaderId.HasValue && invitedStudentIds.Contains(proposedLeaderId.Value)
                    ? new[] { proposedLeaderId.Value }
                    : [];
                await AddForStudentIdsAsync(message, invitedLeaderIds, "Team invitation as Team Leader",
                    $"You have been invited to join team \"{invitedTeamName}\" as Team Leader. " +
                    "Open My Team and respond within 24 hours.", cancellationToken);
                await AddForStudentIdsAsync(message, invitedStudentIds.Except(invitedLeaderIds).ToArray(), "Team invitation",
                    $"You have been invited to join team \"{invitedTeamName}\". Open My Team and respond within 24 hours.",
                    cancellationToken);
                break;
            case "TeamFormation.Accepted.v1":
                await AddForStudentIdsAsync(message, ReadGuidAsArray(data, "creatorStudentId"),
                    "Team invitation accepted", "A member accepted your team invitation.", cancellationToken);
                break;
            case "TeamFormation.Declined.v1":
                await AddForStudentIdsAsync(message, ReadGuidAsArray(data, "creatorStudentId"),
                    "Team invitation declined",
                    "A member declined your team invitation. You can invite another student or finalize without them.",
                    cancellationToken);
                break;
            case "TeamFormation.Left.v1":
                await AddForStudentIdsAsync(message, ReadGuidAsArray(data, "creatorStudentId"),
                    "A member left your team",
                    ReadBoolean(data, "wasProposedLeader")
                        ? "Your proposed Team Leader left. Choose a new Team Leader when you finalize the team."
                        : "An accepted member left your team formation. You can invite another student.",
                    cancellationToken);
                break;
            case "TeamFormation.InvitationExpired.v1":
                await AddForStudentIdsAsync(message, ReadGuidAsArray(data, "creatorStudentId"),
                    "Team invitation expired",
                    "An invitation was not answered within 24 hours and expired. You can invite the student again.",
                    cancellationToken);
                await AddForStudentsAsync(message, data, "studentIds", "Team invitation expired",
                    "Your team invitation expired because it was not answered within 24 hours.", cancellationToken);
                break;
            case "TeamFormation.Closed.v1":
                await AddForStudentsAsync(message, data, "studentIds", "Team invitation closed",
                    "The team was finalized before you responded, so this invitation is closed.", cancellationToken);
                break;
            case "TeamFormation.Cancelled.v1":
                await AddForStudentsAsync(message, data, "studentIds", "Team formation cancelled",
                    "This team formation was cancelled. Open My Team for details.", cancellationToken);
                break;
            case "TeamFormation.Completed.v1":
                await AddForStudentsAsync(message, data, "studentIds", "Your team is ready",
                    "All members accepted. Your team is now active.", cancellationToken);
                break;
            case "Team.Created.v1" when ReadString(data, "source") == "ClassManager":
                await AddForUsersAsync(message, data, "studentUserIds", NotificationType.SystemAnnouncement,
                    "Your team is ready",
                    "Your class manager created your team. Open My Team to view it.", cancellationToken);
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
                var isProfileChangeSubmission = ReadBoolean(data, "isProjectProfileChangeProposal");
                await AddForOptionalUserAsync(message, data, "lecturerUserId", NotificationType.ProjectDirectionSubmitted,
                    isProfileChangeSubmission ? "Project Profile change awaiting review" : "Project direction awaiting review",
                    isProfileChangeSubmission
                        ? "A team submitted changes to its Project name or Description for your review."
                        : "A team submitted its project direction for your review.", cancellationToken);
                var lecturerUserId = ReadGuid(data, "lecturerUserId");
                if (lecturerUserId.HasValue) realtimeNotificationRecipients = [lecturerUserId.Value];
                realtimeNotificationTeamId = ReadGuid(data, "teamId");
                break;
            case "CheckpointSubmission.Submitted.v1":
                var submissionVersion = data.TryGetProperty("submissionVersion", out var versionValue) && versionValue.TryGetInt32(out var parsedVersion)
                    ? parsedVersion
                    : 1;
                var checkpointNumber = data.TryGetProperty("checkpointNumber", out var checkpointValue) && checkpointValue.TryGetInt32(out var parsedCheckpoint)
                    ? parsedCheckpoint
                    : 0;
                var isResubmission = submissionVersion > 1;
                var checkpointLabel = checkpointNumber > 0 ? $"Checkpoint {checkpointNumber}" : "a checkpoint";
                await AddForOptionalUserAsync(
                    message, data, "lecturerUserId", NotificationType.SubmissionSubmitted,
                    isResubmission ? $"{checkpointLabel} resubmitted" : $"New {checkpointLabel} submission",
                    isResubmission
                        ? $"A team submitted version {submissionVersion} of {checkpointLabel} for your review."
                        : $"A team submitted materials for {checkpointLabel} for your review.",
                    cancellationToken);
                break;
            case "ProjectDirection.Reviewed.v1":
                var directionDecision = ReadString(data, "decision");
                var isProfileChangeReview = ReadBoolean(data, "isProjectProfileChangeProposal");
                realtimeNotificationRecipients = ReadGuids(data, "studentUserIds");
                realtimeNotificationTeamId = ReadGuid(data, "teamId");
                foreach (var userId in realtimeNotificationRecipients)
                {
                    var directionNotificationType = directionDecision switch
                    {
                        "Approved" => NotificationType.ProjectDirectionApproved,
                        "Rejected" => NotificationType.ProjectDirectionRejected,
                        _ => NotificationType.ProjectDirectionNeedsRevision
                    };
                    await AddAsync(message, userId,
                        directionNotificationType,
                        isProfileChangeReview && directionDecision == "Rejected"
                            ? "Project Profile change rejected"
                            : isProfileChangeReview ? "Project Profile change reviewed" : "Project direction reviewed",
                        isProfileChangeReview
                            ? directionDecision == "Approved"
                                ? "Your proposed Project Profile changes were approved and are now applied."
                                : directionDecision == "Rejected"
                                    ? "Your proposed Project Profile changes were rejected. The approved profile remains unchanged."
                                    : "Your proposed Project Profile changes need revision. The approved profile remains unchanged."
                            : $"Your project direction was reviewed: {directionDecision}.", cancellationToken);
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

    private async Task PublishClassMajorUpdatedAsync(
        Guid classId,
        Guid studentId,
        string majorCode,
        CancellationToken cancellationToken)
    {
        var recipients = await GetClassRealtimeRecipientsAsync(classId, cancellationToken);
        if (recipients.Length == 0) return;

        await _classRealtimePublisher.PublishMajorUpdatedAsync(
            recipients,
            classId,
            studentId,
            majorCode,
            cancellationToken);
    }

    private async Task<Guid[]> GetClassRealtimeRecipientsAsync(
        Guid classId,
        CancellationToken cancellationToken)
    {
        var studentRecipients = await _context.ClassStudents.AsNoTracking()
            .Where(enrollment =>
                enrollment.ClassId == classId &&
                (enrollment.EnrollmentStatus == EnrollmentStatus.Active ||
                 enrollment.EnrollmentStatus == EnrollmentStatus.Completed) &&
                enrollment.Student.UserId.HasValue)
            .Select(enrollment => enrollment.Student.UserId!.Value)
            .ToArrayAsync(cancellationToken);
        var assignedLecturers = await _context.ClassLecturers.AsNoTracking()
            .Where(assignment => assignment.ClassId == classId)
            .Select(assignment => assignment.LecturerId)
            .ToArrayAsync(cancellationToken);
        var primaryLecturerId = await _context.Classes.AsNoTracking()
            .Where(item => item.Id == classId)
            .Select(item => item.PrimaryLecturerId)
            .SingleOrDefaultAsync(cancellationToken);
        return studentRecipients
            .Concat(assignedLecturers)
            .Concat(primaryLecturerId.HasValue ? [primaryLecturerId.Value] : [])
            .Distinct()
            .ToArray();
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

    private async Task AddCheckpointDeadlineNotificationsAsync(
        OutboxMessage message,
        JsonElement data,
        bool isReminder,
        CancellationToken cancellationToken)
    {
        var checkpointId = ReadGuid(data, "checkpointId");
        if (!checkpointId.HasValue ||
            !data.TryGetProperty("checkpointNumber", out var numberValue) ||
            !numberValue.TryGetInt32(out var number) ||
            !data.TryGetProperty("startDateUtc", out var startValue) ||
            !startValue.TryGetDateTime(out var start) ||
            !data.TryGetProperty("endDateUtc", out var endValue) ||
            !endValue.TryGetDateTime(out var end))
            return;

        var schedule = await _context.ClassCheckpointSchedules.AsNoTracking()
            .Where(item => item.ClassId == message.AggregateId && item.CheckpointId == checkpointId.Value)
            .Select(item => new { item.StartDateUtc, item.EndDateUtc, item.Class.ClassCode })
            .SingleOrDefaultAsync(cancellationToken);
        // A newer schedule supersedes both pending reminders and unprocessed change events.
        // PostgreSQL timestamps have microsecond precision; the payload may have finer ticks.
        if (schedule is null || Math.Abs((schedule.StartDateUtc - start).Ticks) > 10 ||
            Math.Abs((schedule.EndDateUtc - end).Ticks) > 10 || DateTime.UtcNow > schedule.EndDateUtc)
            return;

        if (isReminder)
        {
            await AddCheckpointTeamNotificationsAsync(message, checkpointId.Value, number, schedule.ClassCode,
                schedule.EndDateUtc, false, cancellationToken);
            return;
        }

        var recipients = await _context.ClassStudents.AsNoTracking()
            .Where(item => item.ClassId == message.AggregateId &&
                item.EnrollmentStatus == EnrollmentStatus.Active &&
                item.Student.Status == StudentStatus.Active &&
                item.Student.UserId.HasValue && item.Student.User != null &&
                item.Student.User.Status == UserStatus.Active)
            .Select(item => item.Student.UserId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var deadline = schedule.EndDateUtc.ToString("dd/MM/yyyy HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        var label = $"Checkpoint {number}";
        var isReopen = ReadBoolean(data, "isReopen");
        var title = isReopen ? $"{label} deadline reopened" : $"{label} schedule updated";
        var body = isReopen
            ? $"Your lecturer reopened {label} for class {schedule.ClassCode}. New deadline: {deadline}. Open your workspace to submit."
            : $"{label} for class {schedule.ClassCode} is scheduled. Deadline: {deadline}. Open your workspace for the full schedule.";
        foreach (var recipient in recipients)
        {
            await AddAsync(message, recipient, NotificationType.SystemAnnouncement, title, body, cancellationToken);
        }
    }

    private async Task AddCheckpointOverdueNotificationsAsync(OutboxMessage message, JsonElement data, CancellationToken cancellationToken)
    {
        var checkpointId = ReadGuid(data, "checkpointId");
        if (!checkpointId.HasValue || !data.TryGetProperty("checkpointNumber", out var numberValue) ||
            !numberValue.TryGetInt32(out var number) || !data.TryGetProperty("endDateUtc", out var endValue) ||
            !endValue.TryGetDateTime(out var end)) return;
        var schedule = await _context.ClassCheckpointSchedules.AsNoTracking().Where(item =>
            item.ClassId == message.AggregateId && item.CheckpointId == checkpointId.Value)
            .Select(item => new { item.EndDateUtc, item.Class.ClassCode }).SingleOrDefaultAsync(cancellationToken);
        if (schedule is null || Math.Abs((schedule.EndDateUtc - end).Ticks) > 10 || DateTime.UtcNow < schedule.EndDateUtc) return;
        await AddCheckpointTeamNotificationsAsync(message, checkpointId.Value, number, schedule.ClassCode,
            schedule.EndDateUtc, true, cancellationToken);
    }

    private async Task AddCheckpointTeamNotificationsAsync(OutboxMessage message, Guid checkpointId, int checkpointNumber,
        string classCode, DateTime deadlineUtc, bool overdue, CancellationToken cancellationToken)
    {
        var submittedTeamIds = await _context.Submissions.AsNoTracking().Where(item => item.CheckpointId == checkpointId &&
            item.Team.ClassId == message.AggregateId && item.SubmittedAt.HasValue &&
            (item.Status == SubmissionStatus.Submitted || item.Status == SubmissionStatus.Approved))
            .Select(item => item.TeamId).Distinct().ToArrayAsync(cancellationToken);
        var members = await _context.TeamMembers.AsNoTracking().Where(item => item.ClassId == message.AggregateId &&
            item.CountsTowardActiveTeam && item.Team.Status == TeamStatus.Active &&
            item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && item.ClassStudent.Student.UserId.HasValue &&
            item.ClassStudent.Student.User != null && item.ClassStudent.Student.User.Status == UserStatus.Active &&
            !submittedTeamIds.Contains(item.TeamId)).Select(item => new DeadlineRecipient(
                item.TeamId, item.Team.TeamName, item.ClassStudent.Student.UserId!.Value,
                item.ClassStudent.Student.User!.Email, item.ClassStudent.Student.FullName,
                item.RoleInTeam == TeamMemberRole.Leader)).ToArrayAsync(cancellationToken);
        var deadline = deadlineUtc.ToString("dd/MM/yyyy HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        var label = $"Checkpoint {checkpointNumber}";
        foreach (var recipient in members)
        {
            var title = overdue ? "Your team missed a checkpoint deadline" : $"{label} deadline approaching";
            var body = overdue
                ? $"Team {recipient.TeamName} in class {classCode} missed {label}. The deadline was {deadline}."
                : $"Team {recipient.TeamName} in class {classCode} has one day left to submit {label}. Deadline: {deadline}.";
            var payload = JsonSerializer.Serialize(new { data = new
            {
                teamId = recipient.TeamId, checkpointId, checkpointNumber, classId = message.AggregateId,
                classCode, teamName = recipient.TeamName, deadlineUtc, isOverdue = overdue,
                isTeamLeader = recipient.IsTeamLeader
            } }, JsonOptions);
            var workspaceLink = $"/student/workspace/{recipient.TeamId}";
            await AddAsync(message, recipient.UserId, overdue ? NotificationType.DeadlineOverdue : NotificationType.DeadlineReminder,
                title, body, cancellationToken, workspaceLink, payload);
            if (!string.IsNullOrWhiteSpace(recipient.Email))
            {
                var subject = overdue ? $"E-HUB: {label} deadline missed" : $"E-HUB: {label} is due in one day";
                await QueueClassEmailAsync(message, recipient.Email, recipient.FullName, subject, title,
                    $"{body}\n\nOpen your workspace: {BuildFrontendUrl(workspaceLink)}", cancellationToken);
            }
        }
    }

    private async Task AddDeadlineExtensionRequestNotificationsAsync(OutboxMessage message, JsonElement data, CancellationToken cancellationToken)
    {
        var primaryLecturerIds = await _context.Classes.AsNoTracking().Where(item => item.Id == message.AggregateId)
            .Select(item => item.PrimaryLecturerId).Where(item => item.HasValue).Select(item => item!.Value).ToArrayAsync(cancellationToken);
        var assignedLecturerIds = await _context.ClassLecturers.AsNoTracking().Where(item => item.ClassId == message.AggregateId)
            .Select(item => item.LecturerId).ToArrayAsync(cancellationToken);
        var recipients = await _context.Users.AsNoTracking().Where(item => primaryLecturerIds.Concat(assignedLecturerIds).Contains(item.Id) &&
            item.Status == UserStatus.Active).Select(item => new { item.Id, item.Email, item.FullName }).ToArrayAsync(cancellationToken);
        var teamName = ReadString(data, "teamName");
        var classCode = ReadString(data, "classCode");
        var checkpointNumber = data.TryGetProperty("checkpointNumber", out var number) && number.TryGetInt32(out var parsed) ? parsed : 0;
        var link = $"/lecturer/classes?tab=checkpoint&classId={message.AggregateId}&checkpointNumber={checkpointNumber}";
        var title = "Deadline extension requested";
        var body = $"Team {teamName} in class {classCode} requested that you reopen Checkpoint {checkpointNumber}.";
        foreach (var recipient in recipients)
        {
            await AddAsync(message, recipient.Id, NotificationType.DeadlineExtensionRequested, title, body, cancellationToken, link);
            if (!string.IsNullOrWhiteSpace(recipient.Email))
                await QueueClassEmailAsync(message, recipient.Email, recipient.FullName,
                    "E-HUB: deadline extension requested", title, $"{body}\n\nSign in to E-HUB: {BuildFrontendUrl("/login")}", cancellationToken);
        }
    }

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

    public async Task PublishAfterCommitAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Type == "Class.MajorUpdated.v1")
        {
            using var majorDocument = JsonDocument.Parse(message.PayloadJson);
            if (!majorDocument.RootElement.TryGetProperty("data", out var majorData)) return;
            var studentId = ReadGuid(majorData, "studentId");
            var majorCode = ReadString(majorData, "majorCode");
            if (studentId.HasValue && !string.IsNullOrWhiteSpace(majorCode))
                await PublishClassMajorUpdatedAsync(
                    message.AggregateId,
                    studentId.Value,
                    majorCode,
                    cancellationToken);
            return;
        }

        if (IsClassMajorsChangedEvent(message.Type))
        {
            var majorRecipients = await GetClassRealtimeRecipientsAsync(message.AggregateId, cancellationToken);
            if (majorRecipients.Length > 0)
                await _classRealtimePublisher.PublishMajorsChangedAsync(
                    majorRecipients,
                    message.AggregateId,
                    message.Type,
                    cancellationToken);
            return;
        }

        if (message.Type == "Team.Created.v1")
        {
            using var teamDocument = JsonDocument.Parse(message.PayloadJson);
            if (!teamDocument.RootElement.TryGetProperty("data", out var teamData) ||
                ReadString(teamData, "source") != "ClassManager") return;
            var teamId = ReadGuid(teamData, "teamId");
            var teamRecipients = ReadGuids(teamData, "studentUserIds");
            if (teamId.HasValue && teamRecipients.Length > 0)
                await _classRealtimePublisher.PublishTeamCreatedAsync(
                    teamRecipients, message.AggregateId, teamId.Value, cancellationToken);
            return;
        }

        if (message.Type is not ("TeamFormation.Invited.v1" or "TeamFormation.Accepted.v1" or
            "TeamFormation.Declined.v1" or "TeamFormation.Left.v1" or "TeamFormation.InvitationExpired.v1" or
            "TeamFormation.Closed.v1" or "TeamFormation.Cancelled.v1" or "TeamFormation.Completed.v1")) return;
        using var document = JsonDocument.Parse(message.PayloadJson);
        if (!document.RootElement.TryGetProperty("data", out var data)) return;
        var formationId = ReadGuid(data, "formationId");
        if (!formationId.HasValue) return;
        // A pending formation changes availability for every student in the class,
        // not only for the invited members. Notify all class participants so their
        // roster can immediately disable or release the affected students.
        var recipients = await GetClassRealtimeRecipientsAsync(message.AggregateId, cancellationToken);
        if (recipients.Length > 0)
            await _classRealtimePublisher.PublishTeamFormationChangedAsync(recipients, message.AggregateId, formationId.Value, cancellationToken);
    }

    private static bool IsClassMajorsChangedEvent(string eventType) => eventType is
        "Class.EnrollmentMajorCorrected.v1" or
        "Class.EnrollmentMajorsVerified.v1" or
        "Class.EnrollmentMajorsSynchronizedFromFile.v1" or
        "Class.StudentProfileMajorsSynchronized.v1";

    private async Task AddForStudentsAsync(
        OutboxMessage message,
        JsonElement data,
        string propertyName,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        await AddForStudentIdsAsync(message, ReadGuids(data, propertyName), title, body, cancellationToken);
    }

    private async Task AddForStudentIdsAsync(
        OutboxMessage message,
        Guid[] studentIds,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        if (studentIds.Length == 0) return;
        var userIds = await _context.Students.AsNoTracking()
            .Where(item => studentIds.Contains(item.Id) && item.UserId.HasValue)
            .Select(item => item.UserId!.Value).Distinct().ToArrayAsync(cancellationToken);
        foreach (var userId in userIds)
            await AddAsync(message, userId, NotificationType.SystemAnnouncement, title, body, cancellationToken);
    }

    private async Task QueueClassCreatedEmailAsync(
        OutboxMessage message,
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

        await QueueClassEmailAsync(
            message, lecturer.Email, lecturer.FullName,
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

    private async Task QueueStudentImportEmailsAsync(
        OutboxMessage message,
        JsonElement data,
        ClassEmailDetails classDetails,
        CancellationToken cancellationToken)
    {
        if (!data.TryGetProperty("studentRecipients", out var recipients) || recipients.ValueKind != JsonValueKind.Array) return;

        foreach (var recipient in recipients.EnumerateArray())
        {
            var email = ReadString(recipient, "email");
            if (string.IsNullOrWhiteSpace(email)) continue;

            await QueueClassEmailAsync(
                message, email, ReadString(recipient, "fullName"),
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

    private async Task QueueClassEmailAsync(
        OutboxMessage source,
        string email,
        string fullName,
        string subject,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        // One durable delivery per source event and recipient, including replays of
        // a source event after a crash. The existing unique event_id index is the guard.
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var key = $"{ClassEmailEventType}:{source.EventId:N}:{normalizedEmail}";
        var eventId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
        if (_context.OutboxMessages.Local.Any(item => item.EventId == eventId)
            || await _context.OutboxMessages.AnyAsync(item => item.EventId == eventId, cancellationToken))
            return;

        _context.OutboxMessages.Add(new OutboxMessage
        {
            EventId = eventId,
            Type = ClassEmailEventType,
            AggregateType = source.AggregateType,
            AggregateId = source.AggregateId,
            OccurredAtUtc = source.OccurredAtUtc,
            AvailableAtUtc = source.OccurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(new
            {
                sourceEventId = source.EventId,
                data = new { email = normalizedEmail, fullName, subject, title, body }
            }, JsonOptions)
        });
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
        CancellationToken cancellationToken,
        string? linkOverride = null,
        string? dataJsonOverride = null)
    {
        if (_context.Notifications.Local.Any(notification =>
                notification.SourceEventId == message.EventId && notification.RecipientUserId == recipientUserId)
            || await _context.Notifications.IgnoreQueryFilters().AnyAsync(notification =>
                notification.SourceEventId == message.EventId && notification.RecipientUserId == recipientUserId, cancellationToken))
            return;

        var link = linkOverride ?? await BuildLinkAsync(message, cancellationToken);
        _context.Notifications.Add(new Notification
        {
            SourceEventId = message.EventId,
            RecipientUserId = recipientUserId,
            Type = type,
            Title = title,
            Body = body,
            Link = link,
            DataJson = dataJsonOverride ?? message.PayloadJson,
            CreatedAt = message.OccurredAtUtc
        });
    }

    private async Task<string?> BuildLinkAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (message.Type == "CheckpointSubmission.Submitted.v1")
        {
            var teamId = ReadPayloadGuid(message.PayloadJson, "teamId");
            return teamId.HasValue ? $"/workspace/teams/{teamId.Value}" : null;
        }

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

        if (message.Type == "ProjectDirection.Reviewed.v1"
            && ReadPayloadBoolean(message.PayloadJson, "isProjectProfileChangeProposal"))
        {
            var teamId = ReadPayloadGuid(message.PayloadJson, "teamId");
            return teamId.HasValue ? $"/student/workspace/{teamId.Value}" : "/student/workspace";
        }

        return message.Type switch
        {
            "AccountApproval.Requested.v1" => "/admin/account-approvals",
            "TeamProposal.Submitted.v1" => $"/classes/{message.AggregateId}",
            "TeamProposal.Reviewed.v1" or "ProjectDirection.Reviewed.v1" or
                "Class.EnrollmentMajorsAutoLocked.v1" => $"/student/classes/{message.AggregateId}",
            "TeamFormation.Invited.v1" or "TeamFormation.Accepted.v1" or
                "TeamFormation.Declined.v1" or "TeamFormation.Left.v1" or "TeamFormation.InvitationExpired.v1" or
                "TeamFormation.Closed.v1" or
                "TeamFormation.Cancelled.v1" or "TeamFormation.Completed.v1" => "/student/team",
            "Team.MentorAssignmentChanged.v1" => "/mentor/dashboard",
            CheckpointDeadlineEvents.ScheduleChanged or CheckpointDeadlineEvents.DeadlineReminder or CheckpointDeadlineEvents.DeadlineOverdue => "/student/workspace",
            _ => null
        };
    }

    private string BuildFrontendUrl(string path)
    {
        var baseUrl = _frontendOptions.BaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? path : $"{baseUrl}{path}";
    }

    private sealed record DeadlineRecipient(Guid TeamId, string TeamName, Guid UserId, string? Email, string FullName, bool IsTeamLeader);

    private static Guid? ReadPayloadGuid(string payloadJson, string propertyName)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty(propertyName, out var property)
            && property.TryGetGuid(out var value)
                ? value
                : null;
    }

    private static bool ReadPayloadBoolean(string payloadJson, string propertyName)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("data", out var data)
            && ReadBoolean(data, propertyName);
    }

    private static string ReadString(JsonElement data, string propertyName) =>
        data.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static Guid? ReadGuid(JsonElement data, string propertyName) =>
        data.TryGetProperty(propertyName, out var value) && value.TryGetGuid(out var parsed)
            ? parsed
            : null;

    private static Guid[] ReadGuidAsArray(JsonElement data, string propertyName) =>
        ReadGuid(data, propertyName) is { } value ? [value] : [];

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
