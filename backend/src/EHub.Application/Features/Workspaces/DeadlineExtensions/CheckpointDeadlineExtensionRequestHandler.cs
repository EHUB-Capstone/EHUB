using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Checkpoints.LecturerManagement;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.DeadlineExtensions;

public sealed class CheckpointDeadlineExtensionRequestHandler(
    IApplicationDbContext context,
    IDateTimeProvider clock) : ICheckpointDeadlineExtensionRequestHandler
{
    public async Task<Result<CheckpointDeadlineExtensionRequestResponse>> GetMineAsync(
        Guid teamId,
        int checkpointNumber,
        DateTime deadlineUtc,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(role, SystemRoles.Student, StringComparison.OrdinalIgnoreCase)) return Denied("You do not have access to this request.");
        var request = await context.CheckpointDeadlineExtensionRequests.AsNoTracking()
            .Where(item => item.TeamId == teamId && item.RequestedById == userId && item.Checkpoint.CheckpointNumber == checkpointNumber &&
                item.DeadlineUtc == deadlineUtc &&
                item.Team.TeamMembers.Any(member => member.CountsTowardActiveTeam && member.RoleInTeam == TeamMemberRole.Leader &&
                    member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && member.ClassStudent.Student.UserId == userId))
            .Select(item => new CheckpointDeadlineExtensionRequestResponse
            {
                Id = item.Id, TeamId = item.TeamId, TeamName = item.Team.TeamName,
                ClassId = item.ClassId, ClassCode = item.Class.ClassCode, CheckpointId = item.CheckpointId,
                CheckpointNumber = item.Checkpoint.CheckpointNumber, CheckpointTitle = item.Checkpoint.Name,
                DeadlineUtc = item.DeadlineUtc, RequestedAtUtc = item.RequestedAtUtc, Reason = item.Reason
            }).FirstOrDefaultAsync(cancellationToken);
        return request is null
            ? Result.Failure<CheckpointDeadlineExtensionRequestResponse>(ErrorCodes.CommonNotFoundError, "No deadline extension request was found.")
            : Result.Success(request);
    }

    public async Task<Result<CheckpointDeadlineExtensionRequestResponse>> CreateAsync(
        Guid teamId,
        int checkpointNumber,
        CreateCheckpointDeadlineExtensionRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(role, SystemRoles.Student, StringComparison.OrdinalIgnoreCase))
            return Denied("Only the active team leader can request a deadline extension.");

        var team = await context.Teams
            .Include(item => item.Class)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .FirstOrDefaultAsync(item => item.Id == teamId && item.Status == TeamStatus.Active, cancellationToken);
        if (team is null) return Denied("You do not have access to this team.");

        var leader = team.TeamMembers.SingleOrDefault(item =>
            item.CountsTowardActiveTeam && item.RoleInTeam == TeamMemberRole.Leader &&
            item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && item.ClassStudent.Student.UserId == userId);
        if (leader is null) return Denied("Only the active team leader can request a deadline extension.");

        var checkpoint = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item =>
            item.CourseId == team.Class.CourseId && item.ClassId == null &&
            item.CheckpointNumber == checkpointNumber && item.Status != CheckpointStatus.Archived, cancellationToken);
        if (checkpoint is null)
            return Result.Failure<CheckpointDeadlineExtensionRequestResponse>(ErrorCodes.WorkspaceNotFound, "The checkpoint was not found for this team.");

        var schedule = await context.ClassCheckpointSchedules.AsNoTracking().FirstOrDefaultAsync(item =>
            item.ClassId == team.ClassId && item.CheckpointId == checkpoint.Id, cancellationToken);
        var now = clock.UtcNow;
        if (schedule is null || now <= schedule.EndDateUtc)
            return Result.Failure<CheckpointDeadlineExtensionRequestResponse>(ErrorCodes.WorkspaceDeadlineExtensionNotOverdue, "A deadline extension can only be requested after the checkpoint deadline.");

        var submitted = await context.Submissions.AsNoTracking().AnyAsync(item =>
            item.TeamId == team.Id && item.CheckpointId == checkpoint.Id && item.SubmittedAt.HasValue &&
            (item.Status == SubmissionStatus.Submitted || item.Status == SubmissionStatus.Approved), cancellationToken);
        if (submitted)
            return Result.Failure<CheckpointDeadlineExtensionRequestResponse>(ErrorCodes.WorkspaceValidationError, "Your team has already submitted this checkpoint.");

        var existing = await context.CheckpointDeadlineExtensionRequests.AnyAsync(item =>
            item.TeamId == team.Id && item.CheckpointId == checkpoint.Id && item.DeadlineUtc == schedule.EndDateUtc, cancellationToken);
        if (existing)
            return Result.Failure<CheckpointDeadlineExtensionRequestResponse>(ErrorCodes.WorkspaceDeadlineExtensionAlreadyRequested, "Your team has already requested an extension for this deadline.");

        var deadlineRequest = new CheckpointDeadlineExtensionRequest
        {
            TeamId = team.Id,
            ClassId = team.ClassId,
            CheckpointId = checkpoint.Id,
            RequestedById = userId,
            DeadlineUtc = schedule.EndDateUtc,
            RequestedAtUtc = now,
            Reason = request.Reason.Trim(),
            CreatedBy = userId
        };
        context.CheckpointDeadlineExtensionRequests.Add(deadlineRequest);
        ClassOutbox.Enqueue(context, CheckpointDeadlineEvents.DeadlineExtensionRequested, team.ClassId, new
        {
            requestId = deadlineRequest.Id,
            teamId = team.Id,
            teamName = team.TeamName,
            checkpointId = checkpoint.Id,
            checkpointNumber,
            checkpointTitle = checkpoint.Name,
            classCode = team.Class.ClassCode,
            deadlineUtc = schedule.EndDateUtc,
            reason = deadlineRequest.Reason
        }, now);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CheckpointDeadlineExtensionRequestResponse
        {
            Id = deadlineRequest.Id,
            TeamId = team.Id,
            TeamName = team.TeamName,
            ClassId = team.ClassId,
            ClassCode = team.Class.ClassCode,
            CheckpointId = checkpoint.Id,
            CheckpointNumber = checkpointNumber,
            CheckpointTitle = checkpoint.Name,
            DeadlineUtc = schedule.EndDateUtc,
            RequestedAtUtc = now,
            Reason = deadlineRequest.Reason
        });
    }

    private static Result<CheckpointDeadlineExtensionRequestResponse> Denied(string message) =>
        Result.Failure<CheckpointDeadlineExtensionRequestResponse>(ErrorCodes.WorkspaceAccessDenied, message);
}
