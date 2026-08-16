using System;
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

namespace EHub.Application.Features.Classes.UpdateClassSchedule;

public sealed class UpdateClassScheduleCommandHandler : IUpdateClassScheduleCommandHandler
{
    private const string ScheduleChanged = "SCHEDULE_CHANGED";
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateClassScheduleCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ClassResponse>> HandleAsync(
        Guid classId,
        UpdateClassScheduleRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to update class schedule.");
        }

        var validationError = ClassScheduleRules.Validate(request.Schedules);
        if (validationError != null)
        {
            return Failure(ErrorCodes.ClassValidationError, validationError);
        }

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                transactionCancellationToken => UpdateWithinTransactionAsync(
                    classId,
                    request,
                    currentUserId,
                    isLecturer,
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
            return Failure(ErrorCodes.ClassScheduleConflict, "The schedule conflicted with another concurrent update. Reload and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(ErrorCodes.ClassScheduleConflict, "The schedule conflicted with another concurrent update. Reload and try again.");
        }
    }

    private async Task<Result<ClassResponse>> UpdateWithinTransactionAsync(
        Guid classId,
        UpdateClassScheduleRequest request,
        Guid currentUserId,
        bool isLecturer,
        uint expectedVersion,
        CancellationToken cancellationToken)
    {
        var targetClass = await _context.Classes
            .Include(@class => @class.Course)
            .Include(@class => @class.Semester)
            .Include(@class => @class.PrimaryLecturer)
            .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);

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

        if (isLecturer && targetClass.PrimaryLecturerId != currentUserId)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only update schedule for classes assigned to you.");
        }

        var normalizedSchedules = ClassScheduleRules.Normalize(request.Schedules!);
        var otherClasses = await _context.Classes
            .AsNoTracking()
            .Where(@class =>
                @class.SemesterId == targetClass.SemesterId &&
                @class.Id != targetClass.Id &&
                (@class.Status == ClassStatus.Draft || @class.Status == ClassStatus.Active) &&
                @class.ScheduleJson != null)
            .Select(@class => new
            {
                @class.ClassCode,
                @class.PrimaryLecturerId,
                @class.Room,
                @class.ScheduleJson
            })
            .ToListAsync(cancellationToken);

        foreach (var slot in normalizedSchedules)
        {
            var slotRoom = NormalizeRoom(slot.Room, targetClass.Room);
            foreach (var otherClass in otherClasses)
            {
                foreach (var otherSlot in ClassScheduleRules.Deserialize(otherClass.ScheduleJson))
                {
                    if (otherSlot.DayOfWeek != slot.DayOfWeek || otherSlot.SlotNumber != slot.SlotNumber)
                    {
                        continue;
                    }

                    if (targetClass.PrimaryLecturerId.HasValue &&
                        otherClass.PrimaryLecturerId == targetClass.PrimaryLecturerId)
                    {
                        return Failure(
                            ErrorCodes.ClassScheduleConflict,
                            $"Primary lecturer already teaches class '{otherClass.ClassCode}' on {slot.DayOfWeek} Slot {slot.SlotNumber}.");
                    }

                    var otherRoom = NormalizeRoom(otherSlot.Room, otherClass.Room);
                    if (!string.IsNullOrEmpty(slotRoom) &&
                        !string.IsNullOrEmpty(otherRoom) &&
                        string.Equals(slotRoom, otherRoom, StringComparison.OrdinalIgnoreCase))
                    {
                        return Failure(
                            ErrorCodes.ClassScheduleConflict,
                            $"Room '{slotRoom}' is occupied by class '{otherClass.ClassCode}' on {slot.DayOfWeek} Slot {slot.SlotNumber}.");
                    }
                }
            }
        }

        var previousSchedules = ClassScheduleRules.Deserialize(targetClass.ScheduleJson);
        targetClass.ScheduleJson = ClassScheduleRules.Serialize(normalizedSchedules);
        targetClass.Status = ClassScheduleRules.DetermineOperationalStatus(targetClass.PrimaryLecturerId, targetClass.ScheduleJson);
        targetClass.UpdatedBy = currentUserId;

        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = targetClass.Id,
            Action = ScheduleChanged,
            PerformedByUserId = currentUserId,
            OccurredAtUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                PreviousSchedules = previousSchedules,
                NewSchedules = normalizedSchedules
            })
        });

        await _context.SaveChangesAsync(cancellationToken);

        var studentCount = await _context.ClassStudents.CountAsync(
            enrollment => enrollment.ClassId == targetClass.Id && enrollment.EnrollmentStatus == EnrollmentStatus.Active,
            cancellationToken);
        var teamCount = await _context.Teams.CountAsync(
            team => team.ClassId == targetClass.Id && team.Status == TeamStatus.Active,
            cancellationToken);

        return Result.Success(new ClassResponse
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
            Schedules = normalizedSchedules,
            IsEnrollmentMajorLocked = targetClass.IsEnrollmentMajorLocked,
            Status = targetClass.Status.ToString(),
            StudentCount = studentCount,
            TeamCount = teamCount,
            CreatedAtUtc = targetClass.CreatedAt,
            RowVersion = targetClass.Version.ToString()
        });
    }

    private static string? NormalizeRoom(string? slotRoom, string? classRoom)
    {
        var room = string.IsNullOrWhiteSpace(slotRoom) ? classRoom : slotRoom;
        return string.IsNullOrWhiteSpace(room) ? null : room.Trim();
    }

    private static Result<ClassResponse> Failure(string code, string message) =>
        Result.Failure<ClassResponse>(new Error(code, message));
}
