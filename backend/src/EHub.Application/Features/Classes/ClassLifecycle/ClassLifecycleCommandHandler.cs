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

namespace EHub.Application.Features.Classes.ClassLifecycle;

public sealed class ClassLifecycleCommandHandler : IClassLifecycleCommandHandler
{
    private const string ArchivedAction = "CLASS_ARCHIVED";
    private const string RestoredAction = "CLASS_RESTORED";
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClassLifecycleCommandHandler> _logger;

    public ClassLifecycleCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<ClassLifecycleCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task<Result<ClassLifecycleResponse>> ArchiveAsync(
        Guid classId,
        ChangeClassLifecycleRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(classId, request, currentUserId, currentUserRole, shouldArchive: true, cancellationToken);

    public Task<Result<ClassLifecycleResponse>> RestoreAsync(
        Guid classId,
        ChangeClassLifecycleRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(classId, request, currentUserId, currentUserRole, shouldArchive: false, cancellationToken);

    private async Task<Result<ClassLifecycleResponse>> ExecuteAsync(
        Guid classId,
        ChangeClassLifecycleRequest request,
        Guid currentUserId,
        string currentUserRole,
        bool shouldArchive,
        CancellationToken cancellationToken)
    {
        var isStaff = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isStaff)
            return Failure(ErrorCodes.ClassAccessDenied, "Only Admin or Lecturer can archive or restore classes.");

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
            return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 3 or > 500)
            return Failure(ErrorCodes.ClassValidationError, "Reason must contain between 3 and 500 characters.");

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                transactionToken => ChangeWithinTransactionAsync(
                    classId, expectedVersion, reason, currentUserId, shouldArchive, transactionToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class changed while the lifecycle operation was running. Reload and try again.");
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Class lifecycle database conflict for {ClassId}", classId);
            return Failure(ErrorCodes.ClassRestoreInvalid, "The class lifecycle change conflicts with current academic data. Reload and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "Another class update completed first. Reload and try again.");
        }
    }

    private async Task<Result<ClassLifecycleResponse>> ChangeWithinTransactionAsync(
        Guid classId,
        uint expectedVersion,
        string reason,
        Guid currentUserId,
        bool shouldArchive,
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

        // Both lifecycle commands are idempotent. A retry after a lost HTTP response is a no-op.
        if (shouldArchive && targetClass.Status == ClassStatus.Archived ||
            !shouldArchive && targetClass.Status != ClassStatus.Archived)
            return Result.Success(ToResponse(targetClass));

        if (targetClass.Version != expectedVersion)
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class was changed by another user. Reload and try again.");

        var now = DateTime.UtcNow;
        if (shouldArchive)
        {
            var previousStatus = targetClass.Status;
            targetClass.StatusBeforeArchive = previousStatus;
            targetClass.Status = ClassStatus.Archived;
            targetClass.ArchivedAtUtc = now;
            targetClass.ArchivedByUserId = currentUserId;
            targetClass.IsEnrollmentMajorLocked = false;
            targetClass.UpdatedBy = currentUserId;

            AddAuditAndEvent(targetClass, ArchivedAction, "Class.Archived.v1", previousStatus, ClassStatus.Archived, reason, currentUserId, now);
        }
        else
        {
            var validationError = await ValidateRestoreAsync(targetClass, cancellationToken);
            if (validationError != null)
                return Failure(ErrorCodes.ClassRestoreInvalid, validationError);

            var restoredStatus = targetClass.StatusBeforeArchive ??
                ClassScheduleRules.DetermineOperationalStatus(targetClass.PrimaryLecturerId, targetClass.ScheduleJson);
            if (restoredStatus == ClassStatus.Archived)
                restoredStatus = ClassStatus.Draft;

            targetClass.Status = restoredStatus;
            targetClass.ArchivedAtUtc = null;
            targetClass.ArchivedByUserId = null;
            targetClass.StatusBeforeArchive = null;
            targetClass.UpdatedBy = currentUserId;

            AddAuditAndEvent(targetClass, RestoredAction, "Class.Restored.v1", ClassStatus.Archived, restoredStatus, reason, currentUserId, now);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Class lifecycle changed for {ClassId}: {LifecycleAction} by {UserId}",
            targetClass.Id,
            shouldArchive ? "Archived" : "Restored",
            currentUserId);

        return Result.Success(ToResponse(targetClass));
    }

    private async Task<string?> ValidateRestoreAsync(Class targetClass, CancellationToken cancellationToken)
    {
        if (targetClass.Course.Status != CourseStatus.Active)
            return "The subject is inactive and must be activated before this class can be restored.";

        if (targetClass.Semester.Status is SemesterStatus.Completed or SemesterStatus.Archived)
            return "The semester is completed or archived and cannot accept a restored class.";

        if (await _context.Classes.AsNoTracking().AnyAsync(item =>
                item.Id != targetClass.Id &&
                item.SemesterId == targetClass.SemesterId &&
                (item.ClassCode == targetClass.ClassCode ||
                 item.CourseId == targetClass.CourseId && item.ClassIndex == targetClass.ClassIndex), cancellationToken))
            return "The class code or class index is already used in this semester.";

        var intendedStatus = targetClass.StatusBeforeArchive ??
            ClassScheduleRules.DetermineOperationalStatus(targetClass.PrimaryLecturerId, targetClass.ScheduleJson);
        var schedules = ClassScheduleRules.Deserialize(targetClass.ScheduleJson);
        var scheduleValidation = ClassScheduleRules.Validate(schedules);
        if (scheduleValidation != null)
            return scheduleValidation;

        if (intendedStatus == ClassStatus.Active && schedules.Count == 0)
            return "An active class must have at least one schedule slot before it can be restored.";

        if (targetClass.PrimaryLecturerId.HasValue)
        {
            if (targetClass.PrimaryLecturer == null ||
                targetClass.PrimaryLecturer.Status != UserStatus.Active ||
                !targetClass.PrimaryLecturer.UserRoles.Any(item =>
                    string.Equals(item.Role.Name, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase)))
                return "The assigned lecturer is inactive or no longer has the Lecturer role.";

            var hasPrimaryAssignment = await _context.ClassLecturers.AsNoTracking().AnyAsync(item =>
                item.ClassId == targetClass.Id &&
                item.LecturerId == targetClass.PrimaryLecturerId &&
                item.IsPrimary, cancellationToken);
            if (!hasPrimaryAssignment)
                return "The primary lecturer relationship is missing or inconsistent.";
        }
        else if (intendedStatus == ClassStatus.Active)
        {
            return "An active class must have one active primary lecturer before it can be restored.";
        }

        var otherClasses = await _context.Classes.AsNoTracking()
            .Where(item => item.Id != targetClass.Id &&
                item.SemesterId == targetClass.SemesterId &&
                item.Status != ClassStatus.Archived &&
                item.ScheduleJson != null)
            .Select(item => new { item.ClassCode, item.PrimaryLecturerId, item.Room, item.ScheduleJson })
            .ToListAsync(cancellationToken);

        foreach (var slot in schedules)
        {
            var room = NormalizeRoom(slot.Room, targetClass.Room);
            foreach (var otherClass in otherClasses)
            foreach (var otherSlot in ClassScheduleRules.Deserialize(otherClass.ScheduleJson))
            {
                if (slot.DayOfWeek != otherSlot.DayOfWeek || slot.SlotNumber != otherSlot.SlotNumber)
                    continue;

                if (targetClass.PrimaryLecturerId.HasValue && otherClass.PrimaryLecturerId == targetClass.PrimaryLecturerId)
                    return $"The lecturer already teaches class '{otherClass.ClassCode}' at {slot.DayOfWeek}, Slot {slot.SlotNumber}.";

                var otherRoom = NormalizeRoom(otherSlot.Room, otherClass.Room);
                if (room != null && otherRoom != null && string.Equals(room, otherRoom, StringComparison.OrdinalIgnoreCase))
                    return $"Room '{room}' is occupied by class '{otherClass.ClassCode}' at {slot.DayOfWeek}, Slot {slot.SlotNumber}.";
            }
        }

        return null;
    }

    private void AddAuditAndEvent(
        Class targetClass,
        string action,
        string eventType,
        ClassStatus previousStatus,
        ClassStatus newStatus,
        string reason,
        Guid currentUserId,
        DateTime occurredAt)
    {
        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = targetClass.Id,
            Action = action,
            PerformedByUserId = currentUserId,
            OccurredAtUtc = occurredAt,
            DetailsJson = JsonSerializer.Serialize(new { PreviousStatus = previousStatus, NewStatus = newStatus, Reason = reason })
        });
        ClassOutbox.Enqueue(_context, eventType, targetClass.Id, new
        {
            PreviousStatus = previousStatus.ToString(),
            NewStatus = newStatus.ToString(),
            Reason = reason,
            PerformedByUserId = currentUserId
        }, occurredAt);
    }

    private static ClassLifecycleResponse ToResponse(Class targetClass) => new()
    {
        ClassId = targetClass.Id,
        Status = targetClass.Status.ToString(),
        ArchivedAtUtc = targetClass.ArchivedAtUtc,
        RowVersion = targetClass.Version.ToString()
    };

    private static string? NormalizeRoom(string? slotRoom, string? defaultRoom)
    {
        var value = string.IsNullOrWhiteSpace(slotRoom) ? defaultRoom : slotRoom;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Result<ClassLifecycleResponse> Failure(string code, string message) =>
        Result.Failure<ClassLifecycleResponse>(new Error(code, message));
}
