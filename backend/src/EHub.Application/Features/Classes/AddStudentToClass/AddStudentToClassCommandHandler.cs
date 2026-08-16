using System;
using System.Linq;
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

        var validationError = StudentEnrollmentRules.ValidateAndNormalize(
            request.StudentCode,
            request.FullName,
            request.Email,
            request.MajorCode,
            out var input,
            allowMissingMajor: true);
        if (validationError != null)
        {
            return Failure(ErrorCodes.ClassValidationError, validationError);
        }

        var (studentCode, fullName, email, requestedMajorCode) = input;

        var targetClass = await _context.Classes
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

        if (isLecturer && targetClass.PrimaryLecturerId != currentUserId)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You can only add students to classes assigned to you.");
        }

        // Resolve identity before changing any tracked student profile. Code and email may not point to two people.
        var studentByCode = await _context.Students
            .FirstOrDefaultAsync(student =>
                student.NormalizedRollNumber == studentCode || student.RollNumber == studentCode,
                cancellationToken);
        var studentByEmail = await _context.Students
            .FirstOrDefaultAsync(student => student.Email != null && student.Email.ToLower() == email, cancellationToken);

        if (studentByCode != null && studentByEmail != null && studentByCode.Id != studentByEmail.Id)
        {
            return Failure(
                ErrorCodes.ClassStudentIdentityConflict,
                $"Student code '{studentCode}' and email '{email}' are registered to different student profiles. Enter the code and email of the same student.");
        }

        var studentProfile = studentByCode ?? studentByEmail;
        var shouldAssignProfileCode = false;
        if (studentProfile != null)
        {
            if (!string.IsNullOrEmpty(studentProfile.Email) &&
                !string.Equals(studentProfile.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    ErrorCodes.ClassStudentIdentityConflict,
                    $"Student code '{studentCode}' is registered with email '{studentProfile.Email}'. Enter that registered email address.");
            }

            var profileCode = studentProfile.NormalizedRollNumber ?? studentProfile.RollNumber;
            if (string.IsNullOrWhiteSpace(profileCode))
            {
                shouldAssignProfileCode = true;
            }
            else if (!string.Equals(profileCode, studentCode, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    ErrorCodes.ClassStudentIdentityConflict,
                    $"Email '{email}' is registered with student code '{profileCode}'. Enter that registered student code.");
            }
        }

        var profileMajorCode = studentProfile?.MajorCode?.Trim().ToUpperInvariant();
        string enrollmentMajorCode;
        string majorSource;
        if (studentProfile != null && MajorCodes.IsValid(profileMajorCode))
        {
            if (!string.IsNullOrWhiteSpace(requestedMajorCode) &&
                !string.Equals(requestedMajorCode, profileMajorCode, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    ErrorCodes.ClassStudentMajorMismatch,
                    $"Selected major '{requestedMajorCode}' does not match the student's registered major '{profileMajorCode}'. Leave Major blank to use the registered major, or select '{profileMajorCode}'.");
            }

            enrollmentMajorCode = profileMajorCode!;
            majorSource = "StudentProfile";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(requestedMajorCode))
            {
                return Failure(
                    ErrorCodes.ClassValidationError,
                    studentProfile == null
                        ? "Major is required when creating a new student profile."
                        : "The existing student profile has no valid registered major. Select a major for this enrollment.");
            }

            enrollmentMajorCode = requestedMajorCode;
            majorSource = studentProfile == null ? "ManualNewProfile" : "ManualEnrollment";
        }

        ClassStudent? existingEnrollment = null;
        if (studentProfile != null)
        {
            existingEnrollment = await _context.ClassStudents
                .FirstOrDefaultAsync(
                    enrollment => enrollment.ClassId == classId && enrollment.StudentId == studentProfile.Id,
                    cancellationToken);

            if (existingEnrollment != null)
            {
                var code = existingEnrollment.EnrollmentStatus == EnrollmentStatus.Dropped
                    ? ErrorCodes.ClassStudentReEnrollmentRequired
                    : ErrorCodes.ClassStudentAlreadyEnrolled;
                var message = existingEnrollment.EnrollmentStatus == EnrollmentStatus.Dropped
                    ? $"Student '{studentCode}' has a dropped enrollment. Use the explicit re-enroll action."
                    : $"Student '{studentCode}' already has an enrollment in this class.";
                return Failure(
                    code,
                    message);
            }

            var conflictEnrollment = await _context.ClassStudents
                .Include(enrollment => enrollment.Class)
                .AsNoTracking()
                .FirstOrDefaultAsync(enrollment =>
                    enrollment.StudentId == studentProfile.Id &&
                    enrollment.ClassId != classId &&
                    enrollment.Class.CourseId == targetClass.CourseId &&
                    enrollment.Class.SemesterId == targetClass.SemesterId &&
                    enrollment.CountsTowardCourseSemesterLimit,
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
            var matchingUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, cancellationToken);

            studentProfile = new Student
            {
                RollNumber = studentCode,
                NormalizedRollNumber = studentCode,
                FullName = fullName,
                Email = email,
                UserId = matchingUser?.Id,
                MajorCode = enrollmentMajorCode,
                Status = StudentStatus.Active,
                CreatedBy = currentUserId
            };
            await _context.Students.AddAsync(studentProfile, cancellationToken);
        }
        else
        {
            if (shouldAssignProfileCode)
            {
                studentProfile.RollNumber = studentCode;
                studentProfile.NormalizedRollNumber = studentCode;
                studentProfile.UpdatedAt = DateTime.UtcNow;
            }

            if (!studentProfile.UserId.HasValue && !string.IsNullOrWhiteSpace(email))
            {
                var matchingUser = await _context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, cancellationToken);
                if (matchingUser != null)
                {
                    studentProfile.UserId = matchingUser.Id;
                    studentProfile.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
        // Existing Student profile data remains the global source of truth.
        // Enrolling a student must not let a lecturer overwrite that profile.

        existingEnrollment = new ClassStudent
        {
            ClassId = classId,
            StudentId = studentProfile.Id,
            SemesterId = targetClass.SemesterId,
            CourseId = targetClass.CourseId,
            EnrollmentStatus = EnrollmentStatus.Active,
            CountsTowardCourseSemesterLimit = true,
            MajorCodeAtEnrollment = enrollmentMajorCode,
            MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _context.ClassStudents.AddAsync(existingEnrollment, cancellationToken);

        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = classId,
            Action = "STUDENT_ENROLLMENT_ADDED",
            PerformedByUserId = currentUserId,
            OccurredAtUtc = DateTime.UtcNow,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                studentProfile.Id,
                StudentCode = studentCode,
                MajorCodeAtEnrollment = enrollmentMajorCode,
                MajorSource = majorSource,
                Source = "Manual"
            })
        });
        ClassOutbox.Enqueue(_context, "Class.StudentEnrollmentAdded.v1", classId, new
        {
            StudentId = studentProfile.Id,
            Source = "Manual"
        });

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
            MajorCode = existingEnrollment.MajorCodeAtEnrollment,
            ProfileMajorCode = studentProfile.MajorCode,
            MajorVerificationStatus = existingEnrollment.MajorVerificationStatus.ToString(),
            MemberCode = existingEnrollment.MemberCode,
            EnrollmentStatus = existingEnrollment.EnrollmentStatus.ToString(),
            TeamId = null,
            TeamName = null,
            IsTeamLeader = false,
            JoinedAtUtc = existingEnrollment.CreatedAt
        });
    }

    private static Result<ClassStudentDto> Failure(string code, string message) =>
        Result.Failure<ClassStudentDto>(new Error(code, message));
}
