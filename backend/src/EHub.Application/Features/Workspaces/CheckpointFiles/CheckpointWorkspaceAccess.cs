using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointFiles;

internal sealed record WorkspaceAccess(
    Checkpoint Checkpoint,
    Project Project,
    ClassCheckpointSchedule? Schedule,
    Guid ClassId,
    Guid? LecturerUserId,
    bool IsMember,
    string UserName);

/// <summary>Team/checkpoint authorization and schedule rules shared by the checkpoint file handlers.</summary>
internal static class CheckpointWorkspaceAccess
{
    public static async Task<Result<WorkspaceAccess>> ResolveAsync(
        IApplicationDbContext context,
        Guid teamId,
        int checkpointNumber,
        Guid userId,
        string role,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedRole(role)) return Result.Failure<WorkspaceAccess>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var team = await context.Teams.AsNoTracking().Include(item => item.Class).ThenInclude(item => item.Course)
            .Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return Result.Failure<WorkspaceAccess>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var isMember = team.TeamMembers.Any(member => member.CountsTowardActiveTeam && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && member.ClassStudent.Student.UserId == userId);
        var allowed = IsRole(role, SystemRoles.Admin)
            || (IsRole(role, SystemRoles.Lecturer) && (team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId)))
            || (IsRole(role, SystemRoles.Mentor) && team.MentorAssignments.Any(assignment => assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null && assignment.MentorProfile.UserId == userId))
            || (IsRole(role, SystemRoles.Student) && isMember);
        if (!allowed) return Result.Failure<WorkspaceAccess>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var checkpoint = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item => item.CourseId == team.Class.CourseId && item.ClassId == null && item.CheckpointNumber == checkpointNumber && item.Status != CheckpointStatus.Archived, cancellationToken);
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (checkpoint is null || project is null) return Result.Failure<WorkspaceAccess>(ErrorCodes.WorkspaceNotFound, "The checkpoint workspace was not found.");
        var schedule = await context.ClassCheckpointSchedules.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ClassId == team.ClassId && item.CheckpointId == checkpoint.Id, cancellationToken);
        return Result.Success(new WorkspaceAccess(
            checkpoint, project, schedule, team.ClassId, team.Class.PrimaryLecturerId, isMember,
            team.TeamMembers.FirstOrDefault(member => member.ClassStudent.Student.UserId == userId)?.ClassStudent.Student.FullName ?? string.Empty));
    }

    public static Result EnsureOpen(ClassCheckpointSchedule? schedule, DateTime now)
    {
        if (schedule is not null && now >= schedule.StartDateUtc && now <= schedule.EndDateUtc) return Result.Success();
        var message = schedule is null
            ? "This checkpoint has not been scheduled for your class."
            : now < schedule.StartDateUtc
                ? $"This checkpoint opens at {schedule.StartDateUtc:O}."
                : $"This checkpoint closed at {schedule.EndDateUtc:O}.";
        return Result.Failure(ErrorCodes.WorkspaceCheckpointNotOpen, message);
    }

    public static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static bool IsStudent(string role) => IsRole(role, SystemRoles.Student);

    private static bool IsSupportedRole(string role) =>
        IsRole(role, SystemRoles.Admin) || IsRole(role, SystemRoles.Lecturer) ||
        IsRole(role, SystemRoles.Mentor) || IsRole(role, SystemRoles.Student);

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
}
