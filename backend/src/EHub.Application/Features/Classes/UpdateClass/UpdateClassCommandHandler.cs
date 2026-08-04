using System;
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

namespace EHub.Application.Features.Classes.UpdateClass;

public sealed class UpdateClassCommandHandler : IUpdateClassCommandHandler
{
    private const string TeachingAssignmentChanged = "TEACHING_ASSIGNMENT_CHANGED";
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
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to update class information.");
        }

        var targetClass = await LoadClassAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Failure(ErrorCodes.ClassArchived, "Cannot update information of an archived class.");
        }

        if (isLecturer && !IsAssigned(targetClass, currentUserId))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only update classes assigned to you.");
        }

        if (request.Room != null)
        {
            var room = request.Room.Trim();
            if (room.Length > 50)
            {
                return Failure(ErrorCodes.ClassValidationError, "Room must not exceed 50 characters.");
            }

            targetClass.Room = room;
        }

        targetClass.UpdatedBy = currentUserId;

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
        if (!string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only Admin can update teaching assignments.");
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

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Failure(ErrorCodes.ClassArchived, "Cannot update an archived class.");
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

        var previousLecturerId = targetClass.PrimaryLecturerId;
        var assignmentsToRevoke = targetClass.ClassLecturers
            .Where(assignment =>
                assignment.LecturerId != newLecturer?.Id &&
                (assignment.IsPrimary || assignment.LecturerId == previousLecturerId))
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

        try
        {
            // EF Core wraps this multi-entity SaveChanges in one database transaction.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The class was changed by another user. Reload and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The teaching assignment conflicted with another update. Reload and try again.");
        }

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
        targetClass.PrimaryLecturerId == userId ||
        targetClass.ClassLecturers.Any(assignment => assignment.LecturerId == userId);

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
            ScheduleJson = targetClass.ScheduleJson,
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
