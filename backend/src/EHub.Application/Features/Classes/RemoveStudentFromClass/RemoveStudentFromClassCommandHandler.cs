using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.RemoveStudentFromClass;

public sealed class RemoveStudentFromClassCommandHandler : IRemoveStudentFromClassCommandHandler
{
    private readonly IApplicationDbContext _context;

    public RemoveStudentFromClassCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> HandleAsync(
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
            return Result.Failure(
                new Error("Classes.AccessDenied", "You do not have permission to remove students from this class."));
        }

        var targetClass = await _context.Classes
            .Include(c => c.ClassLecturers)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure(
                new Error("Classes.NotFound", "The requested class was not found."));
        }

        if (targetClass.Status == ClassStatus.Archived)
        {
            return Result.Failure(
                new Error("Classes.ClassArchived", "Cannot remove students from an archived class."));
        }

        if (isLecturer)
        {
            var isAssigned = targetClass.PrimaryLecturerId == currentUserId ||
                             targetClass.ClassLecturers.Any(cl => cl.LecturerId == currentUserId);

            if (!isAssigned)
            {
                return Result.Failure(
                    new Error("Classes.AccessDenied", "You can only remove students from classes assigned to you."));
            }
        }

        var classStudent = await _context.ClassStudents
            .Include(cs => cs.TeamMembers)
            .ThenInclude(tm => tm.Team)
            .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.StudentId == studentId, cancellationToken);

        if (classStudent == null)
        {
            return Result.Failure(
                new Error("Classes.StudentNotFound", "Student is not enrolled in this class."));
        }

        // Team Safety Rule 1: Check if student is Team Leader of an active team
        var activeLeaderTeam = classStudent.TeamMembers
            .FirstOrDefault(tm => tm.Team != null && tm.Team.Status == TeamStatus.Active && tm.RoleInTeam == TeamMemberRole.Leader);

        if (activeLeaderTeam != null)
        {
            return Result.Failure(
                new Error("Classes.StudentIsTeamLeader",
                    $"Cannot remove student because they are the leader of active team '{activeLeaderTeam.Team.TeamName}'. Please transfer leadership or update the team first."));
        }

        // Team Safety Rule 2: Check if student is a member of an active team
        var activeMemberTeam = classStudent.TeamMembers
            .FirstOrDefault(tm => tm.Team != null && tm.Team.Status == TeamStatus.Active);

        if (activeMemberTeam != null)
        {
            return Result.Failure(
                new Error("Classes.StudentInActiveTeam",
                    $"Cannot remove student because they are currently a member of active team '{activeMemberTeam.Team.TeamName}'. Please remove the student from the team first."));
        }

        // Soft Removal: Mark status as Dropped
        classStudent.EnrollmentStatus = EnrollmentStatus.Dropped;
        classStudent.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
