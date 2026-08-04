using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
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

namespace EHub.Application.Features.Classes.AddStudentToClass;

public sealed class AddStudentToClassCommandHandler : IAddStudentToClassCommandHandler
{
    private readonly IApplicationDbContext _context;

    private static readonly HashSet<string> ValidMajorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIT_SE", "BIT_IA", "BIT_GD", "BIT_AI", "BIT_IS", "BIT_CS", "BIT_CY", "BIT_DS",
        "BBA_IB", "BBA_MKT", "BBA_HM", "BBA_MC", "BBA_TM", "BBA_FIN", "BBA_HRM", "BBA_DM", "BBA_BA", "BBA_LOG",
        "BLA_ELT", "BLA_BC", "BLA_JP", "BLA_KR", "BLA_CN"
    };

    public AddStudentToClassCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClassStudentDto>> HandleAsync(
        Guid classId,
        AddStudentToClassRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<ClassStudentDto>(
                new Error("Classes.AccessDenied", "You do not have permission to add students to this class."));
        }

        // 2. Input Validation
        if (string.IsNullOrWhiteSpace(request.StudentCode))
        {
            return Result.Failure<ClassStudentDto>(
                new Error("Classes.InvalidStudentCode", "Student code (Roll number) is required."));
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Result.Failure<ClassStudentDto>(
                new Error("Classes.InvalidFullName", "Student full name is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !MailAddress.TryCreate(request.Email.Trim(), out _))
        {
            return Result.Failure<ClassStudentDto>(
                new Error("Classes.InvalidEmail", "A valid student email address is required."));
        }

        var majorCode = request.MajorCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(majorCode) || !ValidMajorCodes.Contains(majorCode))
        {
            return Result.Failure<ClassStudentDto>(
                new Error("Classes.InvalidMajorCode", $"Major code '{request.MajorCode}' is invalid."));
        }

        var studentCode = request.StudentCode.Trim().ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        // 3. Target Class Check
        var targetClass = await _context.Classes
            .Include(c => c.ClassLecturers)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure<ClassStudentDto>(
                new Error("Classes.NotFound", "The requested class was not found."));
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Result.Failure<ClassStudentDto>(
                new Error("Classes.ClassArchived", "Cannot add students to an archived class."));
        }

        if (isLecturer)
        {
            var isAssigned = targetClass.PrimaryLecturerId == currentUserId ||
                             targetClass.ClassLecturers.Any(cl => cl.LecturerId == currentUserId);

            if (!isAssigned)
            {
                return Result.Failure<ClassStudentDto>(
                    new Error("Classes.AccessDenied", "You can only add students to classes assigned to you."));
            }
        }

        // 4. Upsert Student Profile
        var studentProfile = await _context.Students
            .FirstOrDefaultAsync(s => s.NormalizedRollNumber == studentCode || (s.Email != null && s.Email.ToLower() == email), cancellationToken);

        if (studentProfile == null)
        {
            studentProfile = new Student
            {
                RollNumber = studentCode,
                NormalizedRollNumber = studentCode,
                FullName = request.FullName.Trim(),
                Email = email,
                MajorCode = majorCode,
                Status = StudentStatus.Active
            };
            await _context.Students.AddAsync(studentProfile, cancellationToken);
        }
        else
        {
            studentProfile.FullName = request.FullName.Trim();
            studentProfile.Email = email;
            studentProfile.MajorCode = majorCode;
            if (string.IsNullOrEmpty(studentProfile.RollNumber))
            {
                studentProfile.RollNumber = studentCode;
                studentProfile.NormalizedRollNumber = studentCode;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 5. Duplicate Check in SAME Class
        var existingEnrollment = await _context.ClassStudents
            .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentProfile.Id, cancellationToken);

        if (existingEnrollment != null && existingEnrollment.EnrollmentStatus == EnrollmentStatus.Active)
        {
            return Result.Failure<ClassStudentDto>(
                new Error("Classes.StudentAlreadyEnrolled", $"Student '{studentCode}' is already actively enrolled in this class."));
        }

        // 6. Same Subject + Same Term Conflict Check across other classes
        var conflictEnrollment = await _context.ClassStudents
            .Include(cs => cs.Class)
            .AsNoTracking()
            .FirstOrDefaultAsync(cs => cs.StudentId == studentProfile.Id &&
                                       cs.ClassId != classId &&
                                       cs.Class.CourseId == targetClass.CourseId &&
                                       cs.Class.SemesterId == targetClass.SemesterId &&
                                       cs.Class.Status == ClassStatus.Active &&
                                       cs.EnrollmentStatus == EnrollmentStatus.Active, cancellationToken);

        if (conflictEnrollment != null)
        {
            return Result.Failure<ClassStudentDto>(
                new Error("Classes.StudentConflictSameSubjectSemester",
                    $"Student '{studentCode}' is already enrolled in active class '{conflictEnrollment.Class.ClassCode}' for the same subject and academic term."));
        }

        // 7. Add or Re-activate ClassStudent
        if (existingEnrollment == null)
        {
            existingEnrollment = new ClassStudent
            {
                ClassId = classId,
                StudentId = studentProfile.Id,
                EnrollmentStatus = EnrollmentStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.ClassStudents.AddAsync(existingEnrollment, cancellationToken);
        }
        else
        {
            existingEnrollment.EnrollmentStatus = EnrollmentStatus.Active;
            existingEnrollment.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var dto = new ClassStudentDto
        {
            StudentId = studentProfile.Id,
            RollNumber = studentProfile.RollNumber ?? studentCode,
            FullName = studentProfile.FullName,
            Email = studentProfile.Email ?? email,
            MajorCode = studentProfile.MajorCode,
            MemberCode = existingEnrollment.MemberCode,
            EnrollmentStatus = existingEnrollment.EnrollmentStatus.ToString(),
            TeamId = null,
            TeamName = null,
            IsTeamLeader = false,
            JoinedAtUtc = existingEnrollment.CreatedAt
        };

        return Result.Success(dto);
    }
}
