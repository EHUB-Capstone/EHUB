using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

namespace EHub.Application.Features.Classes.UpdateClass;

public sealed class UpdateClassCommandHandler : IUpdateClassCommandHandler
{
    private const string TeachingAssignmentChanged = "TEACHING_ASSIGNMENT_CHANGED";
    private const string ClassInformationChanged = "CLASS_INFORMATION_CHANGED";
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateClassCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ClassResponse>> HandleAsync(
        Guid classId,
        UpdateClassRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to update class information.");
        }

        if (!uint.TryParse(request.RowVersion, out _))
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        }

        if (!request.IsRoomSpecified)
        {
            return Failure(ErrorCodes.ClassValidationError, "room is required; use null explicitly to clear the default room.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                transactionCancellationToken => UpdateInformationWithinTransactionAsync(
                    classId,
                    request,
                    currentUserId,
                    currentUserRole,
                    transactionCancellationToken),
                cancellationToken);
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ClassScheduleConflict, "The room conflicted with another concurrent schedule update. Reload and try again.");
        }
    }

    private async Task<Result<ClassResponse>> UpdateInformationWithinTransactionAsync(
        Guid classId,
        UpdateClassRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to update class information.");
        }

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        }

        var targetClass = await LoadClassAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
        if (mutationError != null)
        {
            return Failure(mutationError.Code, mutationError.Message);
        }

        if (targetClass.Version != expectedVersion)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class was changed by another user. Reload and try again.");
        }

        if (isLecturer && !IsAssigned(targetClass, currentUserId))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only update classes assigned to you.");
        }

        var previousRoom = targetClass.Room;
        if (request.IsRoomSpecified)
        {
            var room = request.Room?.Trim();
            if (room?.Length > 50)
            {
                return Failure(ErrorCodes.ClassValidationError, "Room must not exceed 50 characters.");
            }

            var normalizedRoom = string.IsNullOrWhiteSpace(room) ? null : room;
            var conflictingClassCode = await FindDefaultRoomConflictAsync(
                targetClass,
                normalizedRoom,
                cancellationToken);
            if (conflictingClassCode != null)
            {
                return Failure(
                    ErrorCodes.ClassScheduleConflict,
                    $"Room '{normalizedRoom}' is occupied by class '{conflictingClassCode}' in one or more schedule slots of this class.");
            }

            targetClass.Room = normalizedRoom;
        }

        targetClass.UpdatedBy = currentUserId;

        if (!string.Equals(previousRoom, targetClass.Room, StringComparison.Ordinal))
        {
            _context.ClassAuditLogs.Add(new ClassAuditLog
            {
                ClassId = targetClass.Id,
                Action = ClassInformationChanged,
                PerformedByUserId = currentUserId,
                OccurredAtUtc = DateTime.UtcNow,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    PreviousRoom = previousRoom,
                    NewRoom = targetClass.Room
                })
            });
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class was changed by another user. Reload and try again.");
        }

        return Result.Success(await BuildResponseAsync(targetClass, cancellationToken));
    }

    public async Task<Result<ClassResponse>> UpdateTeachingAssignmentAsync(
        Guid classId,
        UpdateTeachingAssignmentRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!ClassAuthorizationRules.IsAdmin(currentUserRole))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator can update teaching assignments.");
        }

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        }

        if (!request.IsPrimaryLecturerIdSpecified)
        {
            return Failure(ErrorCodes.ClassValidationError, "primaryLecturerId is required; use null explicitly to unassign a Draft class.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                transactionCancellationToken => UpdateTeachingAssignmentWithinTransactionAsync(
                    classId,
                    request,
                    currentUserId,
                    expectedVersion,
                    transactionCancellationToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class was changed by another user. Reload and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The teaching assignment conflicted with another update. Reload and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The teaching assignment conflicted with another update. Reload and try again.");
        }
    }

    private async Task<Result<ClassResponse>> UpdateTeachingAssignmentWithinTransactionAsync(
        Guid classId,
        UpdateTeachingAssignmentRequest request,
        Guid currentUserId,
        uint expectedVersion,
        CancellationToken cancellationToken)
    {

        var targetClass = await LoadClassAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
        if (mutationError != null)
        {
            return Failure(mutationError.Code, mutationError.Message);
        }

        if (targetClass.Version != expectedVersion)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class was changed by another user. Reload and try again.");
        }

        User? newLecturer = null;
        if (request.PrimaryLecturerId.HasValue)
        {
            newLecturer = await _context.Users
                .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .FirstOrDefaultAsync(user => user.Id == request.PrimaryLecturerId.Value, cancellationToken);

            if (newLecturer == null ||
                newLecturer.Status != UserStatus.Active ||
                !newLecturer.UserRoles.Any(userRole =>
                    string.Equals(userRole.Role.Name, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase)))
            {
                return Failure(
                    ErrorCodes.ClassInvalidLecturer,
                    "The specified lecturer does not exist, is inactive, or does not have LECTURER role.");
            }
        }

        if (newLecturer == null && targetClass.Status == ClassStatus.Active)
        {
            return Failure(
                ErrorCodes.ClassLecturerRequired,
                "An active class must have exactly one lecturer. Reassign it atomically instead of unassigning it.");
        }

        if (newLecturer != null)
        {
            var conflictClassCode = await FindLecturerScheduleConflictAsync(
                targetClass,
                newLecturer.Id,
                cancellationToken);

            if (conflictClassCode != null)
            {
                return Failure(
                    ErrorCodes.ClassScheduleConflict,
                    $"Lecturer already teaches class '{conflictClassCode}' in one or more schedule slots of this class.");
            }
        }

        var previousLecturerId = targetClass.PrimaryLecturerId;
        var assignmentsToRevoke = targetClass.ClassLecturers
            .Where(assignment => assignment.LecturerId != newLecturer?.Id)
            .ToList();

        foreach (var assignment in assignmentsToRevoke)
        {
            _context.ClassLecturers.Remove(assignment);
        }

        if (newLecturer != null)
        {
            var existingAssignment = targetClass.ClassLecturers
                .FirstOrDefault(assignment => assignment.LecturerId == newLecturer.Id);

            if (existingAssignment != null)
            {
                existingAssignment.IsPrimary = true;
                existingAssignment.AssignedAt = DateTime.UtcNow;
                existingAssignment.AssignedById = currentUserId;
            }
            else
            {
                _context.ClassLecturers.Add(new ClassLecturer
                {
                    ClassId = targetClass.Id,
                    LecturerId = newLecturer.Id,
                    IsPrimary = true,
                    AssignedAt = DateTime.UtcNow,
                    AssignedById = currentUserId
                });
            }
        }

        targetClass.PrimaryLecturerId = newLecturer?.Id;
        targetClass.PrimaryLecturer = newLecturer;
        targetClass.Status = ClassScheduleRules.DetermineOperationalStatus(newLecturer?.Id, targetClass.ScheduleJson);
        targetClass.UpdatedBy = currentUserId;

        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = targetClass.Id,
            Action = TeachingAssignmentChanged,
            PerformedByUserId = currentUserId,
            OccurredAtUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                PreviousPrimaryLecturerId = previousLecturerId,
                NewPrimaryLecturerId = newLecturer?.Id
            })
        });
        ClassOutbox.Enqueue(_context, "Class.TeachingAssignmentChanged.v1", targetClass.Id, new
        {
            PreviousPrimaryLecturerId = previousLecturerId,
            NewPrimaryLecturerId = newLecturer?.Id
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(await BuildResponseAsync(targetClass, cancellationToken));
    }

    private Task<Class?> LoadClassAsync(Guid classId, CancellationToken cancellationToken) =>
        _context.Classes
            .Include(@class => @class.Course)
            .Include(@class => @class.Semester)
            .Include(@class => @class.PrimaryLecturer)
            .Include(@class => @class.ClassLecturers)
            .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);

    private static bool IsAssigned(Class targetClass, Guid userId) =>
        targetClass.PrimaryLecturerId == userId;

    private async Task<string?> FindLecturerScheduleConflictAsync(
        Class targetClass,
        Guid lecturerId,
        CancellationToken cancellationToken)
    {
        var targetSchedules = ClassScheduleRules.Deserialize(targetClass.ScheduleJson);
        if (targetSchedules.Count == 0)
        {
            return null;
        }

        var otherClasses = await _context.Classes
            .AsNoTracking()
            .Where(@class =>
                @class.Id != targetClass.Id &&
                @class.SemesterId == targetClass.SemesterId &&
                @class.PrimaryLecturerId == lecturerId &&
                (@class.Status == ClassStatus.Draft || @class.Status == ClassStatus.Active) &&
                @class.ScheduleJson != null)
            .Select(@class => new { @class.ClassCode, @class.ScheduleJson })
            .ToListAsync(cancellationToken);

        foreach (var otherClass in otherClasses)
        {
            var otherSchedules = ClassScheduleRules.Deserialize(otherClass.ScheduleJson);
            if (targetSchedules.Any(target => otherSchedules.Any(other =>
                    target.DayOfWeek == other.DayOfWeek &&
                    target.SlotNumber == other.SlotNumber)))
            {
                return otherClass.ClassCode;
            }
        }

        return null;
    }

    private async Task<string?> FindDefaultRoomConflictAsync(
        Class targetClass,
        string? newDefaultRoom,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newDefaultRoom))
        {
            return null;
        }

        var affectedSchedules = ClassScheduleRules.Deserialize(targetClass.ScheduleJson)
            .Where(schedule => string.IsNullOrWhiteSpace(schedule.Room))
            .ToArray();
        if (affectedSchedules.Length == 0)
        {
            return null;
        }

        var otherClasses = await _context.Classes
            .AsNoTracking()
            .Where(@class =>
                @class.Id != targetClass.Id &&
                @class.SemesterId == targetClass.SemesterId &&
                (@class.Status == ClassStatus.Draft || @class.Status == ClassStatus.Active) &&
                @class.ScheduleJson != null)
            .Select(@class => new { @class.ClassCode, @class.Room, @class.ScheduleJson })
            .ToListAsync(cancellationToken);

        foreach (var otherClass in otherClasses)
        {
            foreach (var otherSchedule in ClassScheduleRules.Deserialize(otherClass.ScheduleJson))
            {
                var sameTime = affectedSchedules.Any(schedule =>
                    schedule.DayOfWeek == otherSchedule.DayOfWeek &&
                    schedule.SlotNumber == otherSchedule.SlotNumber);
                var otherRoom = string.IsNullOrWhiteSpace(otherSchedule.Room)
                    ? otherClass.Room
                    : otherSchedule.Room;
                if (sameTime &&
                    !string.IsNullOrWhiteSpace(otherRoom) &&
                    string.Equals(newDefaultRoom, otherRoom.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return otherClass.ClassCode;
                }
            }
        }

        return null;
    }

    private async Task<ClassResponse> BuildResponseAsync(Class targetClass, CancellationToken cancellationToken)
    {
        var studentCount = await _context.ClassStudents.CountAsync(
            enrollment => enrollment.ClassId == targetClass.Id && enrollment.EnrollmentStatus == EnrollmentStatus.Active,
            cancellationToken);
        var teamCount = await _context.Teams.CountAsync(
            team => team.ClassId == targetClass.Id && team.Status == TeamStatus.Active,
            cancellationToken);

        return new ClassResponse
        {
            Id = targetClass.Id,
            ClassCode = targetClass.ClassCode,
            ClassIndex = targetClass.ClassIndex,
            CourseId = targetClass.CourseId,
            SubjectCode = targetClass.Course.Code,
            SubjectName = targetClass.Course.Name,
            SemesterId = targetClass.SemesterId,
            SemesterCode = targetClass.Semester.Code,
            Year = targetClass.Semester.Year,
            PrimaryLecturerId = targetClass.PrimaryLecturerId,
            PrimaryLecturerName = targetClass.PrimaryLecturer?.FullName,
            PrimaryLecturerEmail = targetClass.PrimaryLecturer?.Email,
            Room = targetClass.Room,
            Schedules = ClassScheduleRules.Deserialize(targetClass.ScheduleJson),
            IsEnrollmentMajorLocked = targetClass.IsEnrollmentMajorLocked,
            Status = targetClass.Status.ToString(),
            StudentCount = studentCount,
            TeamCount = teamCount,
            CreatedAtUtc = targetClass.CreatedAt,
            RowVersion = targetClass.Version.ToString()
        };
    }

    private static Result<ClassResponse> Failure(string code, string message) =>
        Result.Failure<ClassResponse>(new Error(code, message));
}
