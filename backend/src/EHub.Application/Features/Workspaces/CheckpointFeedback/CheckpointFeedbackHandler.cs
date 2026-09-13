using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointFeedback;

public sealed class CheckpointFeedbackHandler(IApplicationDbContext context, ICheckpointFeedbackRealtimePublisher realtimePublisher) : ICheckpointFeedbackHandler
{
    public async Task<Result<WorkspaceCheckpointFeedbackResponse>> CreateAsync(Guid teamId, int checkpointNumber, CreateWorkspaceCheckpointFeedbackRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var content = request.Comment?.Trim() ?? string.Empty;
        if (content.Length is < 1 or > 2000) return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceValidationError, "A comment must contain between 1 and 2000 characters.");
        var team = await context.Teams.Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null || !CanAccess(team, userId, role)) return Denied();
        var checkpoint = await context.Checkpoints.FirstOrDefaultAsync(item => item.CourseId == team.Class.CourseId && item.ClassId == null && item.CheckpointNumber == checkpointNumber && item.Status != CheckpointStatus.Archived, cancellationToken);
        var project = await context.Projects.FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (checkpoint is null || project is null) return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceNotFound, "The checkpoint workspace was not found.");
        var submission = await context.Submissions.OrderByDescending(item => item.VersionNumber).ThenByDescending(item => item.CreatedAt).FirstOrDefaultAsync(item => item.TeamId == teamId && item.CheckpointId == checkpoint.Id, cancellationToken);
        var now = DateTime.UtcNow;
        if (submission is null)
        {
            submission = new Submission { ProjectId = project.Id, TeamId = teamId, CheckpointId = checkpoint.Id, Title = checkpoint.Name, Status = SubmissionStatus.Draft, VersionNumber = 1, CreatedAt = now, CreatedBy = userId };
            context.Submissions.Add(submission);
        }
        if (request.ParentFeedbackId.HasValue && !await context.SubmissionFeedbacks.AnyAsync(item => item.Id == request.ParentFeedbackId && item.SubmissionId == submission.Id, cancellationToken))
            return Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceValidationError, "The parent comment does not belong to this checkpoint.");
        var feedback = new SubmissionFeedback { Submission = submission, Content = content, CreatedById = userId, ParentFeedbackId = request.ParentFeedbackId, CreatedAt = now, CreatedBy = userId };
        context.SubmissionFeedbacks.Add(feedback);
        await context.SaveChangesAsync(cancellationToken);
        var user = await context.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role).AsNoTracking().SingleAsync(item => item.Id == userId, cancellationToken);
        var response = new WorkspaceCheckpointFeedbackResponse { Id = feedback.Id, CheckpointNumber = checkpointNumber, Comment = feedback.Content, ParentFeedbackId = feedback.ParentFeedbackId, CreatedAt = now, User = new WorkspaceCheckpointUserResponse { Id = user.Id, Name = user.FullName, Role = ResolveRole(user), AvatarUrl = user.AvatarUrl } };
        var recipients = await RecipientUserIdsAsync(team, cancellationToken);
        await realtimePublisher.PublishAsync(recipients, teamId, response, cancellationToken);
        return Result.Success(response);
    }

    public async Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid feedbackId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var team = await context.Teams.Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null || !CanAccess(team, userId, role)) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var feedback = await context.SubmissionFeedbacks.Include(item => item.Submission)
            .FirstOrDefaultAsync(item => item.Id == feedbackId && item.Submission.TeamId == teamId && item.Submission.Checkpoint.CheckpointNumber == checkpointNumber, cancellationToken);
        if (feedback is null) return Result.Failure(ErrorCodes.CommonNotFoundError, "Feedback was not found.");
        if (!Is(role, SystemRoles.Admin) && feedback.CreatedById != userId) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You can only delete your own feedback.");
        var now = DateTime.UtcNow;
        feedback.IsDeleted = true; feedback.DeletedAt = now; feedback.DeletedBy = userId;
        var replies = await context.SubmissionFeedbacks.Where(item => item.ParentFeedbackId == feedbackId).ToListAsync(cancellationToken);
        foreach (var reply in replies) { reply.ParentFeedbackId = null; reply.UpdatedAt = now; reply.UpdatedBy = userId; }
        await context.SaveChangesAsync(cancellationToken);
        await realtimePublisher.PublishDeletedAsync(await RecipientUserIdsAsync(team, cancellationToken), teamId, checkpointNumber, feedbackId, cancellationToken);
        return Result.Success();
    }

    private async Task<IReadOnlyCollection<Guid>> RecipientUserIdsAsync(Team team, CancellationToken ct)
    {
        var admins = await context.Users.AsNoTracking().Where(user => user.UserRoles.Any(link => link.Role.Name == SystemRoles.Admin)).Select(user => user.Id).ToArrayAsync(ct);
        return admins.Concat(team.TeamMembers.Where(member => member.CountsTowardActiveTeam && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active).Select(member => member.ClassStudent.Student.UserId).Where(id => id.HasValue).Select(id => id!.Value))
            .Concat(team.MentorAssignments.Where(item => item.Status == MentorAssignmentStatus.Active && item.EndedAt == null).Select(item => item.MentorProfile.UserId))
            .Concat(team.Class.ClassLecturers.Select(item => item.LecturerId)).Append(team.Class.PrimaryLecturerId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
    }
    private static bool CanAccess(Team team, Guid userId, string role) => Is(role, SystemRoles.Admin) || (Is(role, SystemRoles.Lecturer) && (team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(item => item.LecturerId == userId))) || (Is(role, SystemRoles.Mentor) && team.MentorAssignments.Any(item => item.Status == MentorAssignmentStatus.Active && item.EndedAt == null && item.MentorProfile.UserId == userId)) || (Is(role, SystemRoles.Student) && team.TeamMembers.Any(item => item.CountsTowardActiveTeam && item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && item.ClassStudent.Student.UserId == userId));
    private static string ResolveRole(User user) => user.UserRoles.Select(item => item.Role.Name).FirstOrDefault(role => Is(role, SystemRoles.Admin) || Is(role, SystemRoles.Lecturer) || Is(role, SystemRoles.Mentor) || Is(role, SystemRoles.Student)) ?? string.Empty;
    private static bool Is(string value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<WorkspaceCheckpointFeedbackResponse> Denied() => Result.Failure<WorkspaceCheckpointFeedbackResponse>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
}
