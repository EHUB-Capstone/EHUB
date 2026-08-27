using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.CreateClass;

public sealed class CreateClassCommandHandler : ICreateClassCommandHandler
{
    private readonly IApplicationDbContext _context;

    public CreateClassCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClassResponse>> HandleAsync(
        CreateClassRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        // Authorization is enforced here as well as at the controller boundary so
        // callers cannot bypass the business rule by invoking the handler directly.
        if (!ClassAuthorizationRules.IsAdmin(currentUserRole))
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassAccessDenied, "Only an administrator can create classes."));
        }

        // Validate the generated class index before reading reference data.
        if (request.ClassIndex is < 1 or > 999)
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassValidationError, "Class index must be between 1 and 999."));
        }

        var normalizedRoom = string.IsNullOrWhiteSpace(request.Room) ? null : request.Room.Trim();
        if (normalizedRoom?.Length > 50)
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassValidationError, "Room must not exceed 50 characters."));
        }

        // Validate the subject selected by the administrator.
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null)
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassValidationError, "The specified subject does not exist."));
        }

        if (course.Status != CourseStatus.Active)
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassValidationError, "The specified subject is inactive."));
        }

        // Validate the semester selected by the administrator.
        var semester = await _context.Semesters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SemesterId, cancellationToken);

        if (semester == null)
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassValidationError, "The specified academic term does not exist."));
        }

        var creationSemesters = await _context.Semesters.AsNoTracking()
            .Where(item => item.Status == SemesterStatus.Active || item.Status == SemesterStatus.Planned)
            .ToListAsync(cancellationToken);
        var isAvailableForCreation = ClassCreationSemesterPolicy.SelectAvailable(
                creationSemesters,
                DateOnly.FromDateTime(DateTime.UtcNow))
            .Any(item => item.Id == semester.Id);
        if (!isAvailableForCreation)
        {
            return Result.Failure<ClassResponse>(
                new Error(
                    ErrorCodes.ClassValidationError,
                    "Classes can only be created in the current active semester or its immediate planned successor."));
        }

        // Lecturer assignment is optional so an administrator can assign it later.
        Guid? targetLecturerId = null;
        User? lecturerUser = null;

        if (request.PrimaryLecturerId.HasValue)
        {
            targetLecturerId = request.PrimaryLecturerId.Value;
            lecturerUser = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == targetLecturerId, cancellationToken);

            if (lecturerUser == null || lecturerUser.Status != UserStatus.Active ||
                !lecturerUser.UserRoles.Any(ur => string.Equals(ur.Role.Name, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase)))
            {
                return Result.Failure<ClassResponse>(
                    new Error(ErrorCodes.ClassInvalidLecturer, "The specified lecturer does not exist, is inactive, or does not have LECTURER role."));
            }

            var isListedForSemester = await _context.SemesterStaffAssignments
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.SemesterId == semester.Id &&
                        item.UserId == lecturerUser.Id &&
                        item.Role == SemesterStaffRole.Lecturer &&
                        item.Status == SemesterStaffStatus.Active,
                    cancellationToken);
            if (!isListedForSemester)
            {
                return Result.Failure<ClassResponse>(
                    new Error(
                        ErrorCodes.ClassInvalidLecturer,
                        "The specified lecturer is not active in this semester's teaching staff list."));
            }
        }

        // 6. Generate ClassCode & Uniqueness Check
        var classCode = $"{course.Code.Trim().ToUpperInvariant()}_{request.ClassIndex}";

        var isCodeDuplicated = await _context.Classes
            .AnyAsync(c => c.SemesterId == request.SemesterId && c.ClassCode == classCode, cancellationToken);

        if (isCodeDuplicated)
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassCodeDuplicated, $"Class code '{classCode}' already exists in this semester."));
        }

        var isIndexDuplicated = await _context.Classes
            .AnyAsync(c => c.SemesterId == request.SemesterId && c.CourseId == request.CourseId && c.ClassIndex == request.ClassIndex, cancellationToken);

        if (isIndexDuplicated)
        {
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassIndexDuplicated, $"Class index {request.ClassIndex} for subject '{course.Code}' already exists in this semester."));
        }

        var slugBase = ClassSlugRules.BuildBaseSlug(semester.Code, course.Code, request.ClassIndex);
        var existingSlugs = await _context.Classes
            .AsNoTracking()
            .Where(c => c.Slug.StartsWith(slugBase))
            .Select(c => c.Slug)
            .ToListAsync(cancellationToken);
        var slug = ClassSlugRules.MakeUnique(slugBase, existingSlugs);
        // 7. Create Entity
        var newClass = new Class
        {
            ClassCode = classCode,
            Slug = slug,
            ClassIndex = request.ClassIndex,
            SemesterId = request.SemesterId,
            CourseId = request.CourseId,
            PrimaryLecturerId = targetLecturerId,
            Room = normalizedRoom,
            // Activation is automatic only after lecturer and schedule are present.
            Status = ClassStatus.Draft,
            CreatedById = currentUserId
        };

        _context.Classes.Add(newClass);

        if (targetLecturerId.HasValue)
        {
            _context.ClassLecturers.Add(new ClassLecturer
            {
                ClassId = newClass.Id,
                LecturerId = targetLecturerId.Value,
                IsPrimary = true,
                AssignedAt = DateTime.UtcNow,
                AssignedById = currentUserId
            });
        }

        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = newClass.Id,
            Action = "CLASS_CREATED",
            PerformedByUserId = currentUserId,
            OccurredAtUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new
            {
                newClass.ClassCode,
                newClass.CourseId,
                newClass.SemesterId,
                newClass.PrimaryLecturerId
            })
        });
        ClassOutbox.Enqueue(_context, "Class.Created.v1", newClass.Id, new
        {
            newClass.ClassCode,
            newClass.PrimaryLecturerId
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _context.ClearChanges();
            return Result.Failure<ClassResponse>(
                new Error(ErrorCodes.ClassCodeDuplicated, "The class conflicts with another class created concurrently in this semester."));
        }

        var response = new ClassResponse
        {
            Id = newClass.Id,
            Slug = newClass.Slug,
            ClassCode = newClass.ClassCode,
            ClassIndex = newClass.ClassIndex,
            CourseId = newClass.CourseId,
            SubjectCode = course.Code,
            SubjectName = course.Name,
            SemesterId = newClass.SemesterId,
            SemesterCode = semester.Code,
            Year = semester.Year,
            PrimaryLecturerId = targetLecturerId,
            PrimaryLecturerName = lecturerUser?.FullName,
            PrimaryLecturerEmail = lecturerUser?.Email,
            Room = newClass.Room,
            Schedules = ClassScheduleRules.Deserialize(newClass.ScheduleJson),
            IsEnrollmentMajorLocked = newClass.IsEnrollmentMajorLocked,
            Status = newClass.Status.ToString(),
            StudentCount = 0,
            TeamCount = 0,
            CreatedAtUtc = newClass.CreatedAt,
            RowVersion = newClass.Version.ToString()
        };

        return Result.Success(response);
    }
}
