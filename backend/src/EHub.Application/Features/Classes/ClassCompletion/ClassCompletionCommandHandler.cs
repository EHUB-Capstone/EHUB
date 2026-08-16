using System.Text.Json;
using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Classes.ClassCompletion;

public sealed class ClassCompletionCommandHandler : IClassCompletionCommandHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClassCompletionCommandHandler> _logger;

    public ClassCompletionCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<ClassCompletionCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ClassCompletionPreviewResponse>> PreviewAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var targetClass = await _context.Classes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null)
            return FailurePreview(ErrorCodes.ClassNotFound, "The requested class was not found.");
        if (!ClassAuthorizationRules.CanManageClass(targetClass.PrimaryLecturerId, currentUserId, currentUserRole))
            return FailurePreview(ErrorCodes.ClassAccessDenied, "You can only complete classes assigned to you.");

        return Result.Success(await BuildPreviewAsync(targetClass, cancellationToken));
    }

    public Task<Result<ClassLifecycleResponse>> CompleteAsync(
        Guid classId,
        ChangeClassLifecycleRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(classId, request, currentUserId, currentUserRole, reopen: false, cancellationToken);

    public Task<Result<ClassLifecycleResponse>> ReopenAsync(
        Guid classId,
        ChangeClassLifecycleRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(classId, request, currentUserId, currentUserRole, reopen: true, cancellationToken);

    private async Task<Result<ClassLifecycleResponse>> ExecuteAsync(
        Guid classId,
        ChangeClassLifecycleRequest request,
        Guid currentUserId,
        string currentUserRole,
        bool reopen,
        CancellationToken cancellationToken)
    {
        if (reopen ? !ClassAuthorizationRules.IsAdmin(currentUserRole) : !ClassAuthorizationRules.IsStaff(currentUserRole))
            return Failure(ErrorCodes.ClassAccessDenied,
                reopen ? "Only an administrator can reopen a completed class." : "Only an administrator or assigned lecturer can complete a class.");
        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
            return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 3 or > 500)
            return Failure(ErrorCodes.ClassValidationError, "Reason must contain between 3 and 500 characters.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                token => ChangeWithinTransactionAsync(
                    classId, expectedVersion, reason, currentUserId, currentUserRole, reopen, token),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class changed concurrently. Reload and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "Another class operation completed first. Reload and try again.");
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Class completion database conflict for {ClassId}", classId);
            return Failure(ErrorCodes.ClassCompletionBlocked, "The class completion conflicts with current academic data. Reload and try again.");
        }
    }

    private async Task<Result<ClassLifecycleResponse>> ChangeWithinTransactionAsync(
        Guid classId,
        uint expectedVersion,
        string reason,
        Guid currentUserId,
        string currentUserRole,
        bool reopen,
        CancellationToken cancellationToken)
    {
        var targetClass = await _context.Classes
            .Include(item => item.Course)
            .Include(item => item.Semester)
            .Include(item => item.PrimaryLecturer)
                .ThenInclude(user => user!.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null)
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");

        if (reopen)
        {
            if (!ClassAuthorizationRules.IsAdmin(currentUserRole))
                return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator can reopen a completed class.");
            if (targetClass.Status == ClassStatus.Active)
                return Result.Success(ToResponse(targetClass));
            if (targetClass.Status != ClassStatus.Completed)
                return Failure(ErrorCodes.ClassCompletionBlocked, "Only a completed class can be reopened.");
        }
        else
        {
            if (!ClassAuthorizationRules.CanManageClass(targetClass.PrimaryLecturerId, currentUserId, currentUserRole))
                return Failure(ErrorCodes.ClassAccessDenied, "You can only complete classes assigned to you.");
            if (targetClass.Status == ClassStatus.Completed)
                return Result.Success(ToResponse(targetClass));
            if (targetClass.Status != ClassStatus.Active)
                return Failure(ErrorCodes.ClassCompletionBlocked, "Only an active class can be completed.");
        }

        if (targetClass.Version != expectedVersion)
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class was changed by another user. Reload and try again.");

        return reopen
            ? await ReopenWithinTransactionAsync(targetClass, reason, currentUserId, cancellationToken)
            : await CompleteWithinTransactionAsync(targetClass, reason, currentUserId, cancellationToken);
    }

    private async Task<Result<ClassLifecycleResponse>> CompleteWithinTransactionAsync(
        Class targetClass,
        string reason,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var preview = await BuildPreviewAsync(targetClass, cancellationToken);
        if (preview.Blockers.Count > 0)
            return Failure(ErrorCodes.ClassCompletionBlocked, string.Join(" ", preview.Blockers));

        var now = DateTime.UtcNow;
        var enrollments = await _context.ClassStudents
            .Where(item => item.ClassId == targetClass.Id && item.EnrollmentStatus == EnrollmentStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var enrollment in enrollments)
        {
            enrollment.EnrollmentStatus = EnrollmentStatus.Completed;
            enrollment.CountsTowardCourseSemesterLimit = true;
            enrollment.CompletedAtUtc = now;
            enrollment.CompletedByUserId = currentUserId;
            enrollment.UpdatedAt = now;
        }

        var openProposals = await _context.TeamProposals
            .Include(item => item.Members)
            .Where(item => item.ClassId == targetClass.Id &&
                (item.Status == TeamProposalStatus.Draft || item.Status == TeamProposalStatus.Pending || item.Status == TeamProposalStatus.NeedsRevision))
            .ToListAsync(cancellationToken);
        foreach (var proposal in openProposals)
        {
            var previous = proposal.Status;
            proposal.Status = TeamProposalStatus.Cancelled;
            proposal.UpdatedBy = currentUserId;
            foreach (var member in proposal.Members)
                member.CountsTowardOpenProposal = false;
            _context.TeamProposalHistory.Add(new TeamProposalHistory
            {
                ProposalId = proposal.Id,
                FromStatus = previous,
                ToStatus = TeamProposalStatus.Cancelled,
                Action = "CancelledByClassCompletion",
                Comment = reason,
                PerformedByUserId = currentUserId,
                OccurredAtUtc = now,
                SnapshotJson = JsonSerializer.Serialize(new { proposal.TeamName, Reason = reason })
            });
        }

        var mentorAssignments = await _context.MentorAssignments
            .Where(item => item.Team.ClassId == targetClass.Id &&
                item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var assignment in mentorAssignments)
        {
            assignment.Status = MentorAssignmentStatus.Ended;
            assignment.EndedAt = now;
            assignment.UpdatedBy = currentUserId;
        }

        var importSessions = await _context.ClassImportSessions
            .Where(item => item.ClassId == targetClass.Id && item.Status != ClassImportSessionStatus.Consumed)
            .ToListAsync(cancellationToken);
        foreach (var session in importSessions)
            session.ExpiresAtUtc = now;

        var chatGroups = await _context.ChatGroups.Where(item => item.ClassId == targetClass.Id).ToListAsync(cancellationToken);
        foreach (var group in chatGroups)
            group.IsReadOnly = true;

        targetClass.Status = ClassStatus.Completed;
        targetClass.CompletedAtUtc = now;
        targetClass.CompletedByUserId = currentUserId;
        targetClass.CompletionReason = reason;
        targetClass.IsEnrollmentMajorLocked = false;
        targetClass.UpdatedBy = currentUserId;

        AddAuditAndEvent(targetClass, "CLASS_COMPLETED", "Class.Completed.v1", ClassStatus.Active, ClassStatus.Completed,
            reason, currentUserId, now, enrollments.Count);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(targetClass));
    }

    private async Task<Result<ClassLifecycleResponse>> ReopenWithinTransactionAsync(
        Class targetClass,
        string reason,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (targetClass.Semester.Status != SemesterStatus.Active)
            return Failure(ErrorCodes.ClassCompletionBlocked, "The semester must be active before this class can be reopened.");
        if (targetClass.Course.Status != CourseStatus.Active)
            return Failure(ErrorCodes.ClassCompletionBlocked, "The subject must be active before this class can be reopened.");
        if (targetClass.PrimaryLecturer == null || targetClass.PrimaryLecturer.Status != UserStatus.Active ||
            !targetClass.PrimaryLecturer.UserRoles.Any(item =>
                string.Equals(item.Role.Name, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase)))
            return Failure(ErrorCodes.ClassCompletionBlocked, "The assigned lecturer is inactive or no longer has the Lecturer role.");

        var hasPrimaryAssignment = await _context.ClassLecturers.AsNoTracking().AnyAsync(item =>
            item.ClassId == targetClass.Id &&
            item.LecturerId == targetClass.PrimaryLecturerId &&
            item.IsPrimary, cancellationToken);
        if (!hasPrimaryAssignment)
            return Failure(ErrorCodes.ClassCompletionBlocked, "The primary lecturer relationship is missing or inconsistent.");

        var schedules = ClassScheduleRules.Deserialize(targetClass.ScheduleJson);
        var scheduleError = ClassScheduleRules.Validate(schedules);
        if (scheduleError != null || schedules.Count == 0)
            return Failure(ErrorCodes.ClassCompletionBlocked, scheduleError ?? "A reopened class must have at least one schedule slot.");

        var otherClasses = await _context.Classes.AsNoTracking()
            .Where(item => item.Id != targetClass.Id && item.SemesterId == targetClass.SemesterId &&
                (item.Status == ClassStatus.Draft || item.Status == ClassStatus.Active) && item.ScheduleJson != null)
            .Select(item => new { item.ClassCode, item.PrimaryLecturerId, item.Room, item.ScheduleJson })
            .ToListAsync(cancellationToken);
        foreach (var slot in schedules)
        foreach (var otherClass in otherClasses)
        foreach (var otherSlot in ClassScheduleRules.Deserialize(otherClass.ScheduleJson))
        {
            if (slot.DayOfWeek != otherSlot.DayOfWeek || slot.SlotNumber != otherSlot.SlotNumber)
                continue;
            if (otherClass.PrimaryLecturerId == targetClass.PrimaryLecturerId)
                return Failure(ErrorCodes.ClassScheduleConflict,
                    $"The lecturer already teaches class '{otherClass.ClassCode}' at {slot.DayOfWeek}, Slot {slot.SlotNumber}.");
            var room = NormalizeRoom(slot.Room, targetClass.Room);
            var otherRoom = NormalizeRoom(otherSlot.Room, otherClass.Room);
            if (room != null && otherRoom != null && string.Equals(room, otherRoom, StringComparison.OrdinalIgnoreCase))
                return Failure(ErrorCodes.ClassScheduleConflict,
                    $"Room '{room}' is occupied by class '{otherClass.ClassCode}' at {slot.DayOfWeek}, Slot {slot.SlotNumber}.");
        }

        var now = DateTime.UtcNow;
        var enrollments = await _context.ClassStudents
            .Where(item => item.ClassId == targetClass.Id && item.EnrollmentStatus == EnrollmentStatus.Completed)
            .ToListAsync(cancellationToken);
        foreach (var enrollment in enrollments)
        {
            enrollment.EnrollmentStatus = EnrollmentStatus.Active;
            enrollment.CountsTowardCourseSemesterLimit = true;
            enrollment.CompletedAtUtc = null;
            enrollment.CompletedByUserId = null;
            enrollment.UpdatedAt = now;
        }

        var chatGroups = await _context.ChatGroups.Where(item => item.ClassId == targetClass.Id).ToListAsync(cancellationToken);
        foreach (var group in chatGroups)
            group.IsReadOnly = false;

        targetClass.Status = ClassStatus.Active;
        targetClass.CompletedAtUtc = null;
        targetClass.CompletedByUserId = null;
        targetClass.CompletionReason = null;
        targetClass.UpdatedBy = currentUserId;
        AddAuditAndEvent(targetClass, "CLASS_REOPENED", "Class.Reopened.v1", ClassStatus.Completed, ClassStatus.Active,
            reason, currentUserId, now, enrollments.Count);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(targetClass));
    }

    private async Task<ClassCompletionPreviewResponse> BuildPreviewAsync(Class targetClass, CancellationToken cancellationToken)
    {
        var activeEnrollments = await _context.ClassStudents.CountAsync(item =>
            item.ClassId == targetClass.Id && item.EnrollmentStatus == EnrollmentStatus.Active, cancellationToken);
        var droppedEnrollments = await _context.ClassStudents.CountAsync(item =>
            item.ClassId == targetClass.Id && item.EnrollmentStatus == EnrollmentStatus.Dropped, cancellationToken);
        var activeMentors = await _context.MentorAssignments.CountAsync(item =>
            item.Team.ClassId == targetClass.Id && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null, cancellationToken);
        var openProposals = await _context.TeamProposals.CountAsync(item => item.ClassId == targetClass.Id &&
            (item.Status == TeamProposalStatus.Draft || item.Status == TeamProposalStatus.Pending || item.Status == TeamProposalStatus.NeedsRevision), cancellationToken);
        var openDirections = await _context.ProjectDirections.CountAsync(item => item.Team.ClassId == targetClass.Id &&
            item.Status != ProjectDirectionStatus.Approved, cancellationToken);
        var processingImports = await _context.ClassImportSessions.CountAsync(item => item.ClassId == targetClass.Id &&
            item.Status == ClassImportSessionStatus.Processing && item.ExpiresAtUtc > DateTime.UtcNow, cancellationToken);
        var scheduledSessions = await _context.MentoringSessions.CountAsync(item =>
            item.MentorAssignment.Team.ClassId == targetClass.Id && item.Status == MentoringSessionStatus.Scheduled, cancellationToken);

        var blockers = new List<string>();
        if (targetClass.Status is not (ClassStatus.Active or ClassStatus.Completed))
            blockers.Add("Only an active class can be completed.");
        if (processingImports > 0)
            blockers.Add("Wait for the in-progress student import to finish before completing the class.");
        if (scheduledSessions > 0)
            blockers.Add("Complete or cancel all scheduled mentoring sessions before completing the class.");
        var warnings = new List<string>();
        if (openProposals > 0)
            warnings.Add($"{openProposals} open team proposal(s) will be cancelled.");
        if (openDirections > 0)
            warnings.Add($"{openDirections} project direction(s) will be retained as read-only in their current state.");
        if (activeMentors > 0)
            warnings.Add($"{activeMentors} active mentor assignment(s) will be ended.");

        return new ClassCompletionPreviewResponse
        {
            ClassId = targetClass.Id,
            ClassCode = targetClass.ClassCode,
            Status = targetClass.Status.ToString(),
            ActiveEnrollmentCount = activeEnrollments,
            DroppedEnrollmentCount = droppedEnrollments,
            ActiveMentorAssignmentCount = activeMentors,
            OpenTeamProposalCount = openProposals,
            OpenProjectDirectionCount = openDirections,
            ProcessingImportSessionCount = processingImports,
            ScheduledMentoringSessionCount = scheduledSessions,
            Blockers = blockers,
            Warnings = warnings,
            RowVersion = targetClass.Version.ToString()
        };
    }

    private void AddAuditAndEvent(
        Class targetClass,
        string action,
        string eventType,
        ClassStatus previousStatus,
        ClassStatus newStatus,
        string reason,
        Guid currentUserId,
        DateTime occurredAt,
        int enrollmentCount)
    {
        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = targetClass.Id,
            Action = action,
            PerformedByUserId = currentUserId,
            OccurredAtUtc = occurredAt,
            DetailsJson = JsonSerializer.Serialize(new
            {
                PreviousStatus = previousStatus,
                NewStatus = newStatus,
                Reason = reason,
                EnrollmentCount = enrollmentCount
            })
        });
        ClassOutbox.Enqueue(_context, eventType, targetClass.Id, new
        {
            PreviousStatus = previousStatus.ToString(),
            NewStatus = newStatus.ToString(),
            Reason = reason,
            EnrollmentCount = enrollmentCount,
            PerformedByUserId = currentUserId
        }, occurredAt);
    }

    private static string? NormalizeRoom(string? slotRoom, string? defaultRoom)
    {
        var value = string.IsNullOrWhiteSpace(slotRoom) ? defaultRoom : slotRoom;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ClassLifecycleResponse ToResponse(Class targetClass) => new()
    {
        ClassId = targetClass.Id,
        Status = targetClass.Status.ToString(),
        CompletedAtUtc = targetClass.CompletedAtUtc,
        ArchivedAtUtc = targetClass.ArchivedAtUtc,
        RowVersion = targetClass.Version.ToString()
    };

    private static Result<ClassLifecycleResponse> Failure(string code, string message) =>
        Result.Failure<ClassLifecycleResponse>(new Error(code, message));

    private static Result<ClassCompletionPreviewResponse> FailurePreview(string code, string message) =>
        Result.Failure<ClassCompletionPreviewResponse>(new Error(code, message));
}
