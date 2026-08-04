using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
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
        // 1. Role Check
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.AccessDenied", "You do not have permission to update class schedule."));
        }

        // 2. Validation Slot Number & Day Of Week
        foreach (var slot in request.Schedules)
        {
            if (slot.SlotNumber is < 1 or > 4)
            {
                return Result.Failure<ClassResponse>(
                    new Error("Classes.InvalidSlotNumber", "Slot number must be between 1 and 4."));
            }

            if (slot.DayOfWeek == DayOfWeek.Sunday)
            {
                return Result.Failure<ClassResponse>(
                    new Error("Classes.InvalidDayOfWeek", "Classes cannot be scheduled on Sunday."));
            }
        }

        // 3. Duplicate Check inside the same request
        var duplicateInRequest = request.Schedules
            .GroupBy(s => new { s.DayOfWeek, s.SlotNumber })
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateInRequest != null)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.DuplicateSlotInRequest", $"Duplicate schedule slot on {duplicateInRequest.Key.DayOfWeek} (Slot {duplicateInRequest.Key.SlotNumber}) in your request."));
        }

        // 4. Fetch Target Class
        var targetClass = await _context.Classes
            .Include(c => c.Course)
            .Include(c => c.Semester)
            .Include(c => c.PrimaryLecturer)
            .Include(c => c.ClassLecturers)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.NotFound", "The requested class was not found."));
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.ClassArchived", "Cannot update schedule of an archived class."));
        }

        if (isLecturer)
        {
            var isAssigned = targetClass.PrimaryLecturerId == currentUserId ||
                             targetClass.ClassLecturers.Any(cl => cl.LecturerId == currentUserId);

            if (!isAssigned)
            {
                return Result.Failure<ClassResponse>(
                    new Error("Classes.AccessDenied", "You can only update schedule for classes assigned to you."));
            }
        }

        // 5. Query other active classes in the same semester for conflict check
        var otherActiveClasses = await _context.Classes
            .AsNoTracking()
            .Where(c => c.SemesterId == targetClass.SemesterId &&
                        c.Id != targetClass.Id &&
                        c.Status == ClassStatus.Active &&
                        !string.IsNullOrEmpty(c.ScheduleJson))
            .ToListAsync(cancellationToken);

        foreach (var slot in request.Schedules)
        {
            var slotRoom = string.IsNullOrWhiteSpace(slot.Room) ? targetClass.Room : slot.Room.Trim();

            foreach (var other in otherActiveClasses)
            {
                if (string.IsNullOrEmpty(other.ScheduleJson)) continue;

                List<ClassScheduleSlotDto>? otherSchedules = null;
                try
                {
                    otherSchedules = JsonSerializer.Deserialize<List<ClassScheduleSlotDto>>(other.ScheduleJson);
                }
                catch
                {
                    continue;
                }

                if (otherSchedules == null) continue;

                foreach (var otherSlot in otherSchedules)
                {
                    if (otherSlot.DayOfWeek == slot.DayOfWeek && otherSlot.SlotNumber == slot.SlotNumber)
                    {
                        // Check Lecturer Schedule Conflict
                        if (targetClass.PrimaryLecturerId.HasValue &&
                            other.PrimaryLecturerId == targetClass.PrimaryLecturerId)
                        {
                            return Result.Failure<ClassResponse>(
                                new Error("Classes.ScheduleConflictLecturer",
                                    $"Lecturer schedule conflict: Primary lecturer already teaches class '{other.ClassCode}' on {slot.DayOfWeek} Slot {slot.SlotNumber}."));
                        }

                        // Check Room Schedule Conflict
                        var otherSlotRoom = string.IsNullOrWhiteSpace(otherSlot.Room) ? other.Room : otherSlot.Room.Trim();
                        if (!string.IsNullOrEmpty(slotRoom) &&
                            !string.IsNullOrEmpty(otherSlotRoom) &&
                            string.Equals(slotRoom, otherSlotRoom, StringComparison.OrdinalIgnoreCase))
                        {
                            return Result.Failure<ClassResponse>(
                                new Error("Classes.ScheduleConflictRoom",
                                    $"Room schedule conflict: Room '{slotRoom}' is occupied by class '{other.ClassCode}' on {slot.DayOfWeek} Slot {slot.SlotNumber}."));
                        }
                    }
                }
            }
        }

        // Save ScheduleJson
        targetClass.ScheduleJson = JsonSerializer.Serialize(request.Schedules);
        targetClass.UpdatedBy = currentUserId;

        await _context.SaveChangesAsync(cancellationToken);

        var studentCount = await _context.ClassStudents.CountAsync(cs => cs.ClassId == targetClass.Id && cs.EnrollmentStatus == EnrollmentStatus.Active, cancellationToken);
        var teamCount = await _context.Teams.CountAsync(t => t.ClassId == targetClass.Id && t.Status == TeamStatus.Active, cancellationToken);

        var response = new ClassResponse
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
            CreatedAtUtc = targetClass.CreatedAt
        };

        return Result.Success(response);
    }
}
