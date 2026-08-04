using System;
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
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to add students to this class.");
        }

        var validationError = ValidateAndNormalize(request, out var studentCode, out var fullName, out var email, out var majorCode);
        if (validationError != null)
        {
            return Failure(ErrorCodes.ClassValidationError, validationError);
        }

        var targetClass = await _context.Classes
            .Include(@class => @class.ClassLecturers)
            .FirstOrDefaultAsync(@class => @class.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Failure(ErrorCodes.ClassArchived, "Cannot add students to an archived class.");
        }

        if (isLecturer &&
            targetClass.PrimaryLecturerId != currentUserId &&
            targetClass.ClassLecturers.All(assignment => assignment.LecturerId != currentUserId))
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only add students to classes assigned to you.");
        }

        // Resolve identity before changing any tracked student profile. Code and email may not point to two people.
        var studentByCode = await _context.Students
            .FirstOrDefaultAsync(student => student.NormalizedRollNumber == studentCode, cancellationToken);
        var studentByEmail = await _context.Students
            .FirstOrDefaultAsync(student => student.Email != null && student.Email.ToLower() == email, cancellationToken);

        if (studentByCode != null && studentByEmail != null && studentByCode.Id != studentByEmail.Id)
        {
            return Failure(
                ErrorCodes.ClassStudentIdentityConflict,
                "Student code and email belong to different student profiles.");
        }

        var studentProfile = studentByCode ?? studentByEmail;
        if (studentProfile != null &&
            !string.IsNullOrWhiteSpace(studentProfile.NormalizedRollNumber) &&
            !string.Equals(studentProfile.NormalizedRollNumber, studentCode, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                ErrorCodes.ClassStudentIdentityConflict,
                "The email is already associated with a different student code.");
        }

        ClassStudent? existingEnrollment = null;
        if (studentProfile != null)
        {
            existingEnrollment = await _context.ClassStudents
                .FirstOrDefaultAsync(
                    enrollment => enrollment.ClassId == classId && enrollment.StudentId == studentProfile.Id,
                    cancellationToken);

            if (existingEnrollment?.EnrollmentStatus == EnrollmentStatus.Active)
            {
                return Failure(
                    ErrorCodes.ClassStudentAlreadyEnrolled,
                    $"Student '{studentCode}' is already actively enrolled in this class.");
            }

            var conflictEnrollment = await _context.ClassStudents
                .Include(enrollment => enrollment.Class)
                .AsNoTracking()
                .FirstOrDefaultAsync(enrollment =>
                    enrollment.StudentId == studentProfile.Id &&
                    enrollment.ClassId != classId &&
                    enrollment.Class.CourseId == targetClass.CourseId &&
                    enrollment.Class.SemesterId == targetClass.SemesterId &&
                    enrollment.Class.Status == ClassStatus.Active &&
                    enrollment.EnrollmentStatus == EnrollmentStatus.Active,
                    cancellationToken);

            if (conflictEnrollment != null)
            {
                return Failure(
                    ErrorCodes.ClassStudentEnrollmentConflict,
                    $"Student '{studentCode}' is already enrolled in active class '{conflictEnrollment.Class.ClassCode}' for the same subject and academic term.");
            }
        }

        // No mutation occurs before all identity and enrollment validation has succeeded.
        if (studentProfile == null)
        {
            studentProfile = new Student
            {
                RollNumber = studentCode,
                NormalizedRollNumber = studentCode,
                FullName = fullName,
                Email = email,
                MajorCode = majorCode,
                Status = StudentStatus.Active,
                CreatedBy = currentUserId
            };
            await _context.Students.AddAsync(studentProfile, cancellationToken);
        }
        else
        {
            studentProfile.RollNumber = studentCode;
            studentProfile.NormalizedRollNumber = studentCode;
            studentProfile.FullName = fullName;
            studentProfile.Email = email;
            studentProfile.MajorCode = majorCode;
            studentProfile.UpdatedBy = currentUserId;
        }

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

        try
        {
            // Student profile and enrollment are committed atomically by a single SaveChanges.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure(
                ErrorCodes.ClassStudentEnrollmentConflict,
                "The student could not be enrolled because the data changed concurrently.");
        }

        return Result.Success(new ClassStudentDto
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
        });
    }

    private static string? ValidateAndNormalize(
        AddStudentToClassRequest request,
        out string studentCode,
        out string fullName,
        out string email,
        out string majorCode)
    {
        studentCode = request.StudentCode?.Trim().ToUpperInvariant() ?? string.Empty;
        fullName = request.FullName?.Trim() ?? string.Empty;
        email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        majorCode = request.MajorCode?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(studentCode) || studentCode.Length > 20)
        {
            return "Student code is required and must not exceed 20 characters.";
        }

        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150)
        {
            return "Student full name is required and must not exceed 150 characters.";
        }

        if (string.IsNullOrWhiteSpace(email) || email.Length > 150 || !MailAddress.TryCreate(email, out _))
        {
            return "A valid student email address is required.";
        }

        if (!MajorCodes.IsValid(majorCode))
        {
            return $"Major code '{request.MajorCode}' is invalid.";
        }

        return null;
    }

    private static Result<ClassStudentDto> Failure(string code, string message) =>
        Result.Failure<ClassStudentDto>(new Error(code, message));
}
