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

namespace EHub.Application.Features.Classes.UpdateClassStudent;

public sealed class UpdateClassStudentCommandHandler : IUpdateClassStudentCommandHandler
{
    private readonly IApplicationDbContext _context;

    public UpdateClassStudentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClassStudentDto>> HandleAsync(
        Guid classId,
        Guid studentId,
        UpdateClassStudentRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!ClassAuthorizationRules.IsStaff(currentUserRole))
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassAccessDenied, "Only an administrator or assigned lecturer can correct enrollment major data."));
        }

        var targetClass = await _context.Classes
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassNotFound, "The requested class was not found."));
        }

        if (!ClassAuthorizationRules.CanManageClass(
                targetClass.PrimaryLecturerId,
                currentUserId,
                currentUserRole))
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassAccessDenied, "You can only correct enrollment major data for classes assigned to you."));
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassArchived, "Cannot update student in an archived class."));
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5 || request.Reason.Trim().Length > 500)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassValidationError, "A correction reason between 5 and 500 characters is required."));
        }

        var classStudent = await _context.ClassStudents
            .Include(cs => cs.Student)
            .Include(cs => cs.TeamMembers)
            .ThenInclude(tm => tm.Team)
            .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentId, cancellationToken);

        if (classStudent == null)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassStudentNotFound, "Student is not enrolled in this class."));
        }

        if (string.IsNullOrWhiteSpace(request.MajorCode))
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassValidationError, "Major code is required."));
        }

        var changed = false;
        var previousMajorCode = classStudent.MajorCodeAtEnrollment;
        if (!string.IsNullOrWhiteSpace(request.MajorCode))
        {
            var normalizedMajorCode = request.MajorCode.Trim().ToUpperInvariant();
            if (!MajorCodes.IsValid(normalizedMajorCode))
            {
                return Result.Failure<ClassStudentDto>(
                    new Error(ErrorCodes.ClassValidationError, $"Major code '{request.MajorCode}' is invalid."));
            }

            if (targetClass.IsEnrollmentMajorLocked &&
                !string.Equals(classStudent.MajorCodeAtEnrollment, normalizedMajorCode, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<ClassStudentDto>(
                    new Error(ErrorCodes.ClassEnrollmentMajorLocked, "Enrollment major is locked for this class."));
            }

            if (!string.Equals(classStudent.MajorCodeAtEnrollment, normalizedMajorCode, StringComparison.OrdinalIgnoreCase))
            {
                classStudent.MajorCodeAtEnrollment = normalizedMajorCode;
                classStudent.MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified;
                classStudent.MajorVerifiedAtUtc = null;
                classStudent.MajorVerifiedByUserId = null;
                changed = true;
            }
        }

        if (changed)
        {
            _context.ClassAuditLogs.Add(new ClassAuditLog
            {
                ClassId = classId,
                Action = "ENROLLMENT_MAJOR_CORRECTED",
                PerformedByUserId = currentUserId,
                OccurredAtUtc = DateTime.UtcNow,
                DetailsJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    StudentId = studentId,
                    PreviousMajorCode = previousMajorCode,
                    NewMajorCode = classStudent.MajorCodeAtEnrollment,
                    Reason = request.Reason.Trim()
                })
            });
            ClassOutbox.Enqueue(_context, "Class.EnrollmentMajorCorrected.v1", classId, new
            {
                StudentId = studentId,
                PreviousMajorCode = previousMajorCode,
                NewMajorCode = classStudent.MajorCodeAtEnrollment,
                Reason = request.Reason.Trim()
            });
        }

        classStudent.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassConcurrencyConflict, "The enrollment changed concurrently. Refresh the roster and try again."));
        }
        catch (DbUpdateException)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassStudentEnrollmentConflict, "The student already has an enrollment for this course in the semester."));
        }

        var activeTeamMember = classStudent.TeamMembers.FirstOrDefault(tm => tm.CountsTowardActiveTeam && tm.Team != null && tm.Team.Status == TeamStatus.Active);

        var dto = new ClassStudentDto
        {
            StudentId = classStudent.StudentId,
            RollNumber = classStudent.Student.RollNumber ?? string.Empty,
            FullName = classStudent.Student.FullName,
            Email = classStudent.Student.Email ?? string.Empty,
            MajorCode = classStudent.MajorCodeAtEnrollment,
            ProfileMajorCode = classStudent.Student.MajorCode,
            MajorVerificationStatus = classStudent.MajorVerificationStatus.ToString(),
            MemberCode = classStudent.MemberCode,
            EnrollmentStatus = classStudent.EnrollmentStatus.ToString(),
            TeamId = activeTeamMember?.TeamId,
            TeamName = activeTeamMember?.Team?.TeamName,
            IsTeamLeader = activeTeamMember?.RoleInTeam == TeamMemberRole.Leader,
            JoinedAtUtc = classStudent.CreatedAt
        };

        return Result.Success(dto);
    }
}
