using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Classes;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.UpdateClassSchedule;

public sealed class UpdateClassScheduleCommandHandler : IUpdateClassScheduleCommandHandler
{
    private const int MaximumScheduleSlots = 12;
    private static readonly JsonSerializerOptions ScheduleJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IApplicationDbContext _context;

    public UpdateClassScheduleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
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

        if (request.Schedules == null || request.Schedules.Count == 0 || request.Schedules.Count > MaximumScheduleSlots)
        {
            return Failure(
                ErrorCodes.ClassValidationError,
                $"Schedules must contain between 1 and {MaximumScheduleSlots} slots.");
        }

        foreach (var slot in request.Schedules)
        {
            if (!Enum.IsDefined(slot.DayOfWeek) || slot.DayOfWeek is DayOfWeek.Sunday)
            {
                return Failure(ErrorCodes.ClassValidationError, "Day of week must be between Monday and Saturday.");
            }

            if (slot.SlotNumber is < 1 or > 4)
            {
                return Failure(ErrorCodes.ClassValidationError, "Slot number must be between 1 and 4.");
            }

            if (slot.Room?.Trim().Length > 50)
            {
                return Failure(ErrorCodes.ClassValidationError, "Room must not exceed 50 characters.");
            }
        }

        var duplicateInRequest = request.Schedules
            .GroupBy(slot => new { slot.DayOfWeek, slot.SlotNumber })
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateInRequest != null)
        {
            return Failure(
                ErrorCodes.ClassValidationError,
                $"Duplicate schedule slot on {duplicateInRequest.Key.DayOfWeek} (Slot {duplicateInRequest.Key.SlotNumber}).");
        }

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        }

        var targetClass = await _context.Classes
            .Include(@class => @class.Course)
            .Include(@class => @class.Semester)
            .Include(@class => @class.PrimaryLecturer)
            .Include(@class => @class.ClassLecturers)
            .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Failure(ErrorCodes.ClassArchived, "Cannot update schedule of an archived class.");
        }

        if (targetClass.Version != expectedVersion)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class was changed by another user. Reload and try again.");
        }

        if (isLecturer &&
            targetClass.PrimaryLecturerId != currentUserId &&
            targetClass.ClassLecturers.All(assignment => assignment.LecturerId != currentUserId))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only update schedule for classes assigned to you.");
        }

        var otherSchedulableClasses = await _context.Classes
            .AsNoTracking()
            .Where(@class =>
                @class.SemesterId == targetClass.SemesterId &&
                @class.Id != targetClass.Id &&
                @class.Status != ClassStatus.Archived &&
                @class.ScheduleJson != null)
            .ToListAsync(cancellationToken);

        foreach (var slot in request.Schedules)
        {
            var slotRoom = NormalizeRoom(slot.Room, targetClass.Room);

            foreach (var otherClass in otherSchedulableClasses)
            {
                var otherSchedules = DeserializeSchedules(otherClass.ScheduleJson);
                foreach (var otherSlot in otherSchedules)
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

        var normalizedSchedules = request.Schedules.Select(slot => new ClassScheduleSlotDto
        {
            DayOfWeek = slot.DayOfWeek,
            SlotNumber = slot.SlotNumber,
            Room = string.IsNullOrWhiteSpace(slot.Room) ? null : slot.Room.Trim()
        }).ToArray();

        targetClass.ScheduleJson = JsonSerializer.Serialize(normalizedSchedules, ScheduleJsonOptions);
        targetClass.Status = targetClass.PrimaryLecturerId.HasValue
            ? ClassStatus.Active
            : ClassStatus.Draft;
        targetClass.UpdatedBy = currentUserId;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class was changed by another user. Reload and try again.");
        }

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
            ScheduleJson = targetClass.ScheduleJson,
            IsEnrollmentMajorLocked = targetClass.IsEnrollmentMajorLocked,
            Status = targetClass.Status.ToString(),
            StudentCount = studentCount,
            TeamCount = teamCount,
            CreatedAtUtc = targetClass.CreatedAt,
            RowVersion = targetClass.Version.ToString()
        });
    }

    private static IReadOnlyCollection<ClassScheduleSlotDto> DeserializeSchedules(string? scheduleJson)
    {
        if (string.IsNullOrWhiteSpace(scheduleJson))
        {
            return Array.Empty<ClassScheduleSlotDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ClassScheduleSlotDto>>(scheduleJson, ScheduleJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<ClassScheduleSlotDto>();
        }
    }

    private static string? NormalizeRoom(string? slotRoom, string? classRoom)
    {
        var room = string.IsNullOrWhiteSpace(slotRoom) ? classRoom : slotRoom;
        return string.IsNullOrWhiteSpace(room) ? null : room.Trim();
    }

    private static Result<ClassResponse> Failure(string code, string message) =>
        Result.Failure<ClassResponse>(new Error(code, message));
}
