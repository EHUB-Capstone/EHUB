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
        // 1. Validation Class Index
        if (request.ClassIndex <= 0)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.InvalidClassIndex", "Class index must be greater than 0."));
        }

        // 2. Role Security Check
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.AccessDenied", "You do not have permission to create a class."));
        }

        // 3. Validate Subject (Course)
        var course = await _context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);

        if (course == null)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.SubjectNotFound", "The specified subject does not exist."));
        }

        if (course.Status != CourseStatus.Active)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.SubjectInactive", "The specified subject is inactive."));
        }

        // 4. Validate AcademicTerm (Semester)
        var semester = await _context.Semesters
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SemesterId, cancellationToken);

        if (semester == null)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.SemesterNotFound", "The specified academic term does not exist."));
        }

        // 5. Lecturer Assignment
        Guid? targetLecturerId = null;
        User? lecturerUser = null;

        if (isLecturer)
        {
            // Lecturer CHỈ ĐƯỢC tạo cho chính mình (Security rule: Không tin LecturerId từ client)
            targetLecturerId = currentUserId;
            lecturerUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId, cancellationToken);
        }
        else if (isAdmin && request.PrimaryLecturerId.HasValue)
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
                    new Error("Classes.InvalidLecturer", "The specified lecturer does not exist, is inactive, or does not have LECTURER role."));
            }
        }

        // 6. Generate ClassCode & Uniqueness Check
        var classCode = $"{course.Code.Trim().ToUpperInvariant()}_{request.ClassIndex}";

        var isCodeDuplicated = await _context.Classes
            .AnyAsync(c => c.SemesterId == request.SemesterId && c.ClassCode == classCode, cancellationToken);

        if (isCodeDuplicated)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.ClassCodeDuplicated", $"Class code '{classCode}' already exists in this semester."));
        }

        var isIndexDuplicated = await _context.Classes
            .AnyAsync(c => c.SemesterId == request.SemesterId && c.CourseId == request.CourseId && c.ClassIndex == request.ClassIndex, cancellationToken);

        if (isIndexDuplicated)
        {
            return Result.Failure<ClassResponse>(
                new Error("Classes.ClassIndexDuplicated", $"Class index {request.ClassIndex} for subject '{course.Code}' already exists in this semester."));
        }

        // 7. Create Entity
        var newClass = new Class
        {
            ClassCode = classCode,
            ClassIndex = request.ClassIndex,
            SemesterId = request.SemesterId,
            CourseId = request.CourseId,
            PrimaryLecturerId = targetLecturerId,
            Room = request.Room?.Trim(),
            Status = ClassStatus.Active,
            CreatedById = currentUserId
        };

        _context.Classes.Add(newClass);

        if (targetLecturerId.HasValue)
        {
            _context.ClassLecturers.Add(new ClassLecturer
            {
                ClassId = newClass.Id,
                LecturerId = targetLecturerId.Value,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        var response = new ClassResponse
        {
            Id = newClass.Id,
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
            ScheduleJson = newClass.ScheduleJson,
            IsEnrollmentMajorLocked = newClass.IsEnrollmentMajorLocked,
            Status = newClass.Status.ToString(),
            StudentCount = 0,
            TeamCount = 0,
            CreatedAtUtc = newClass.CreatedAt
        };

        return Result.Success(response);
    }
}
