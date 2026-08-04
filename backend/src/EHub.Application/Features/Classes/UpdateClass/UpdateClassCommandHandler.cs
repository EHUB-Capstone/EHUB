using System;
using System.Linq;
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

namespace EHub.Application.Features.Classes.UpdateClass;

public sealed class UpdateClassCommandHandler : IUpdateClassCommandHandler
{
    private readonly IApplicationDbContext _context;

    public UpdateClassCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClassResponse>> HandleAsync(
        Guid classId,
        UpdateClassRequest request,
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
                new Error("Classes.AccessDenied", "You do not have permission to update class information."));
        }

        // 2. Fetch Entity
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
                new Error("Classes.ClassArchived", "Cannot update information of an archived class."));
        }

        if (isLecturer)
        {
            // Ownership check for Lecturer
            var isAssigned = targetClass.PrimaryLecturerId == currentUserId ||
                             targetClass.ClassLecturers.Any(cl => cl.LecturerId == currentUserId);

            if (!isAssigned)
            {
                return Result.Failure<ClassResponse>(
                    new Error("Classes.AccessDenied", "You can only update classes assigned to you."));
            }
        }

        // Room update
        if (request.Room != null)
        {
            targetClass.Room = request.Room.Trim();
        }

        // PrimaryLecturer update (Admin ONLY)
        if (isAdmin)
        {
            if (request.PrimaryLecturerId.HasValue)
            {
                var newLecturer = await _context.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == request.PrimaryLecturerId.Value, cancellationToken);

                if (newLecturer == null || newLecturer.Status != UserStatus.Active ||
                    !newLecturer.UserRoles.Any(ur => string.Equals(ur.Role.Name, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase)))
                {
                    return Result.Failure<ClassResponse>(
                        new Error("Classes.InvalidLecturer", "The specified lecturer does not exist, is inactive, or does not have LECTURER role."));
                }

                targetClass.PrimaryLecturerId = newLecturer.Id;
                targetClass.PrimaryLecturer = newLecturer;

                // Sync ClassLecturers
                if (!targetClass.ClassLecturers.Any(cl => cl.LecturerId == newLecturer.Id))
                {
                    _context.ClassLecturers.Add(new ClassLecturer
                    {
                        ClassId = targetClass.Id,
                        LecturerId = newLecturer.Id,
                        AssignedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // Unassign Lecturer (Explicit null)
                targetClass.PrimaryLecturerId = null;
                targetClass.PrimaryLecturer = null;
            }
        }

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

    public async Task<Result<ClassResponse>> UpdateTeachingAssignmentAsync(
        Guid classId,
        UpdateTeachingAssignmentRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.AccessDenied", "Only Admin can update teaching assignments."));
        }

        var updateRequest = new UpdateClassRequest
        {
            PrimaryLecturerId = request.PrimaryLecturerId
        };

        return await HandleAsync(classId, updateRequest, currentUserId, currentUserRole, cancellationToken);
    }
}
