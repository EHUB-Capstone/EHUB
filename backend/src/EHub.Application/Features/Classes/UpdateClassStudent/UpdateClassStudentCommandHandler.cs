using System;
using System.Linq;
using System.Net.Mail;
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
                new Error("Classes.AccessDenied", "You do not have permission to update student information."));
        }

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
                new Error("Classes.ClassArchived", "Cannot update student in an archived class."));
        }

        if (isLecturer)
        {
            var isAssigned = targetClass.PrimaryLecturerId == currentUserId ||
                             targetClass.ClassLecturers.Any(cl => cl.LecturerId == currentUserId);

            if (!isAssigned)
            {
                return Result.Failure<ClassStudentDto>(
                    new Error("Classes.AccessDenied", "You can only update students in classes assigned to you."));
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
                new Error("Classes.StudentNotFound", "Student is not enrolled in this class."));
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            classStudent.Student.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && MailAddress.TryCreate(request.Email.Trim(), out _))
        {
            classStudent.Student.Email = request.Email.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.MajorCode))
        {
            classStudent.Student.MajorCode = request.MajorCode.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.EnrollmentStatus) &&
            Enum.TryParse<EnrollmentStatus>(request.EnrollmentStatus, true, out var newStatus))
        {
            classStudent.EnrollmentStatus = newStatus;
        }

        classStudent.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var activeTeamMember = classStudent.TeamMembers.FirstOrDefault(tm => tm.Team != null && tm.Team.Status == TeamStatus.Active);

        var dto = new ClassStudentDto
        {
            StudentId = classStudent.StudentId,
            RollNumber = classStudent.Student.RollNumber ?? string.Empty,
            FullName = classStudent.Student.FullName,
            Email = classStudent.Student.Email ?? string.Empty,
            MajorCode = classStudent.Student.MajorCode,
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
