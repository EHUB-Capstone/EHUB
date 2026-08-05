using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Classes;
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
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassAccessDenied, "You do not have permission to update student information."));
        }

        var targetClass = await _context.Classes
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassNotFound, "The requested class was not found."));
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassArchived, "Cannot update student in an archived class."));
        }

        if (isLecturer)
        {
            if (targetClass.PrimaryLecturerId != currentUserId)
            {
                return Result.Failure<ClassStudentDto>(
                    new Error(ErrorCodes.ClassAccessDenied, "You can only update students in classes assigned to you."));
            }
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
            }
        }

        if (!string.IsNullOrWhiteSpace(request.EnrollmentStatus))
        {
            if (!Enum.TryParse<EnrollmentStatus>(request.EnrollmentStatus, true, out var newStatus))
            {
                return Result.Failure<ClassStudentDto>(
                    new Error(ErrorCodes.ClassValidationError, $"Enrollment status '{request.EnrollmentStatus}' is invalid."));
            }

            classStudent.EnrollmentStatus = newStatus;
            classStudent.CountsTowardCourseSemesterLimit = newStatus != EnrollmentStatus.Dropped;
        }

        classStudent.UpdatedAt = DateTime.UtcNow;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<ClassStudentDto>(
                new Error(ErrorCodes.ClassStudentEnrollmentConflict, "The student already has an enrollment for this course in the semester."));
        }

        var activeTeamMember = classStudent.TeamMembers.FirstOrDefault(tm => tm.Team != null && tm.Team.Status == TeamStatus.Active);

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
