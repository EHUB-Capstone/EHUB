using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.ReEnrollStudent;

public sealed class ReEnrollStudentCommandHandler : IReEnrollStudentCommandHandler
{
    private readonly IApplicationDbContext _context;

    public ReEnrollStudentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClassStudentDto>> HandleAsync(
        Guid classId,
        Guid studentId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Failure(ErrorCodes.ClassAccessDenied, "You do not have permission to re-enroll students.");
        }

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
            return Failure(ErrorCodes.ClassAccessDenied, "You can only re-enroll students in classes assigned to you.");
        }

        var enrollment = await _context.ClassStudents
            .Include(item => item.Student)
            .Include(item => item.TeamMembers)
            .ThenInclude(member => member.Team)
            .FirstOrDefaultAsync(item => item.ClassId == classId && item.StudentId == studentId, cancellationToken);
        if (enrollment == null)
        {
            return Failure(ErrorCodes.ClassStudentNotFound, "Student is not enrolled in this class.");
        }

        if (enrollment.EnrollmentStatus != EnrollmentStatus.Dropped)
        {
            return Failure(ErrorCodes.ClassStudentNotDropped, "Only a dropped enrollment can be re-enrolled.");
        }

        var conflict = await _context.ClassStudents
            .AsNoTracking()
            .Include(item => item.Class)
            .FirstOrDefaultAsync(item =>
                item.StudentId == studentId &&
                item.ClassId != classId &&
                item.SemesterId == targetClass.SemesterId &&
                item.CourseId == targetClass.CourseId &&
                item.CountsTowardCourseSemesterLimit,
                cancellationToken);
        if (conflict != null)
        {
            return Failure(
                ErrorCodes.ClassStudentEnrollmentConflict,
                $"Student is already enrolled in class '{conflict.Class.ClassCode}' for the same course and semester.");
        }

        enrollment.EnrollmentStatus = EnrollmentStatus.Active;
        enrollment.CountsTowardCourseSemesterLimit = true;
        enrollment.UpdatedAt = DateTime.UtcNow;

        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = classId,
            Action = "STUDENT_RE_ENROLLED",
            PerformedByUserId = currentUserId,
            OccurredAtUtc = DateTime.UtcNow,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { StudentId = studentId })
        });
        ClassOutbox.Enqueue(_context, "Class.StudentReEnrolled.v1", classId, new
        {
            StudentId = studentId
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ClassConcurrencyConflict, "The enrollment changed concurrently. Refresh the roster and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure(ErrorCodes.ClassStudentEnrollmentConflict, "The student already has an enrollment for this course in the semester.");
        }

        var activeTeamMember = enrollment.TeamMembers.FirstOrDefault(member =>
            member.CountsTowardActiveTeam && member.Team != null && member.Team.Status == TeamStatus.Active);
        return Result.Success(new ClassStudentDto
        {
            StudentId = enrollment.StudentId,
            RollNumber = enrollment.Student.RollNumber ?? string.Empty,
            FullName = enrollment.Student.FullName,
            Email = enrollment.Student.Email ?? string.Empty,
            MajorCode = enrollment.MajorCodeAtEnrollment,
            ProfileMajorCode = enrollment.Student.MajorCode,
            MajorVerificationStatus = enrollment.MajorVerificationStatus.ToString(),
            MemberCode = enrollment.MemberCode,
            EnrollmentStatus = enrollment.EnrollmentStatus.ToString(),
            TeamId = activeTeamMember?.TeamId,
            TeamName = activeTeamMember?.Team?.TeamName,
            IsTeamLeader = activeTeamMember?.RoleInTeam == TeamMemberRole.Leader,
            JoinedAtUtc = enrollment.CreatedAt
        });
    }

    private static Result<ClassStudentDto> Failure(string code, string message) =>
        Result.Failure<ClassStudentDto>(new Error(code, message));
}
