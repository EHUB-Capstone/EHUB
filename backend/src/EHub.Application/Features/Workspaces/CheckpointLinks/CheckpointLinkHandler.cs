using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointLinks;

public sealed class CheckpointLinkHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IValidator<SaveWorkspaceCheckpointLinkRequest> validator) : ICheckpointLinkHandler
{
    private const int MaximumActiveLinks = 10;

    public async Task<Result<WorkspaceCheckpointLinkResponse>> CreateAsync(
        Guid teamId, int checkpointNumber, SaveWorkspaceCheckpointLinkRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsStudent(role)) return Denied<WorkspaceCheckpointLinkResponse>();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Invalid<WorkspaceCheckpointLinkResponse>(validation.Errors[0].ErrorMessage);
        CheckpointLinkUrl.TryNormalize(request.Url, out var normalizedUrl);

        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointLinkResponse>(access.Error);
        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var availability = EnsureOpen(access.Value.Schedule, now);
        if (availability.IsFailure) return Result.Failure<WorkspaceCheckpointLinkResponse>(availability.Error);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            int? attemptedVersion = null;
            Guid? reusedDraftId = null;
            uint? observedRowVersion = null;
            try
            {
                var activeLinks = context.SubmissionLinks.Where(link =>
                    link.Submission.TeamId == teamId && link.Submission.CheckpointId == access.Value.Checkpoint.Id);
                if (await activeLinks.CountAsync(cancellationToken) >= MaximumActiveLinks)
                    return Invalid<WorkspaceCheckpointLinkResponse>($"A checkpoint can contain at most {MaximumActiveLinks} active links.");
                if (await activeLinks.AnyAsync(link => link.Url.ToLower() == normalizedUrl.ToLower(), cancellationToken))
                    return Invalid<WorkspaceCheckpointLinkResponse>("This URL has already been submitted for the checkpoint.");

                var latest = await context.Submissions
                    .Include(item => item.RequirementContents)
                    .OrderByDescending(item => item.VersionNumber).ThenByDescending(item => item.CreatedAt)
                    .FirstOrDefaultAsync(item => item.TeamId == teamId && item.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
                var highestFileVersion = await context.SubmissionFiles.IgnoreQueryFilters().AsNoTracking()
                    .Where(item => item.Submission.ProjectId == access.Value.Project.Id && item.Submission.CheckpointId == access.Value.Checkpoint.Id)
                    .MaxAsync(item => (int?)item.VersionNumber, cancellationToken) ?? 0;
                var highestLinkVersion = await context.SubmissionLinks.IgnoreQueryFilters().AsNoTracking()
                    .Where(item => item.Submission.ProjectId == access.Value.Project.Id && item.Submission.CheckpointId == access.Value.Checkpoint.Id)
                    .MaxAsync(item => (int?)item.VersionNumber, cancellationToken) ?? 0;
                var highestArtifactVersion = Math.Max(highestFileVersion, highestLinkVersion);
                var reuseDraft = latest is not null && latest.Status == SubmissionStatus.Draft && latest.SubmittedAt is null &&
                    latest.VersionNumber > highestArtifactVersion &&
                    !await context.SubmissionFiles.AnyAsync(item => item.SubmissionId == latest.Id, cancellationToken) &&
                    !await context.SubmissionLinks.AnyAsync(item => item.SubmissionId == latest.Id, cancellationToken);

                Submission submission;
                if (reuseDraft)
                {
                    submission = latest!;
                    reusedDraftId = submission.Id;
                    observedRowVersion = submission.RowVersion;
                    submission.Status = SubmissionStatus.Submitted;
                    submission.SubmittedAt = now;
                    submission.SubmittedById = userId;
                    submission.UpdatedAt = now;
                    submission.UpdatedBy = userId;
                }
                else
                {
                    var highestSubmissionVersion = await context.Submissions.IgnoreQueryFilters().AsNoTracking()
                        .Where(item => item.ProjectId == access.Value.Project.Id && item.CheckpointId == access.Value.Checkpoint.Id)
                        .MaxAsync(item => (int?)item.VersionNumber, cancellationToken) ?? 0;
                    submission = new Submission
                    {
                        ProjectId = access.Value.Project.Id,
                        TeamId = teamId,
                        CheckpointId = access.Value.Checkpoint.Id,
                        SubmittedById = userId,
                        Title = access.Value.Checkpoint.Name,
                        Status = SubmissionStatus.Submitted,
                        SubmittedAt = now,
                        VersionNumber = Math.Max(highestSubmissionVersion, highestArtifactVersion) + 1,
                        CreatedAt = now,
                        CreatedBy = userId
                    };
                    if (latest is not null)
                    {
                        foreach (var previous in latest.RequirementContents)
                        {
                            submission.RequirementContents.Add(new SubmissionRequirementContent
                            {
                                RequirementIndex = previous.RequirementIndex,
                                Content = previous.Content,
                                CreatedAt = now,
                                CreatedBy = userId
                            });
                        }
                    }
                    context.Submissions.Add(submission);
                }
                attemptedVersion = submission.VersionNumber;

                var link = new SubmissionLink
                {
                    Submission = submission,
                    Name = request.Name.Trim(),
                    Url = normalizedUrl,
                    VersionNumber = submission.VersionNumber,
                    SubmittedById = userId,
                    SubmittedAt = now,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                context.SubmissionLinks.Add(link);
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(Map(link, access.Value.UserName));
            }
            catch (DbUpdateException)
            {
                context.ClearChanges();
                var conflictingVersion = reusedDraftId.HasValue
                    ? await context.Submissions.AsNoTracking().AnyAsync(item => item.Id == reusedDraftId.Value && item.RowVersion != observedRowVersion, cancellationToken)
                    : attemptedVersion.HasValue && await context.Submissions.AsNoTracking().AnyAsync(item =>
                        item.ProjectId == access.Value.Project.Id && item.CheckpointId == access.Value.Checkpoint.Id &&
                        item.VersionNumber == attemptedVersion.Value, cancellationToken);
                if (!conflictingVersion) throw;
            }
        }

        return Result.Failure<WorkspaceCheckpointLinkResponse>(ErrorCodes.WorkspaceConcurrencyConflict,
            "Another submission changed this checkpoint at the same time. Please retry.");
    }

    public async Task<Result<WorkspaceCheckpointLinkResponse>> UpdateAsync(
        Guid teamId, int checkpointNumber, Guid linkId, SaveWorkspaceCheckpointLinkRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsStudent(role)) return Denied<WorkspaceCheckpointLinkResponse>();
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Invalid<WorkspaceCheckpointLinkResponse>(validation.Errors[0].ErrorMessage);
        CheckpointLinkUrl.TryNormalize(request.Url, out var normalizedUrl);

        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointLinkResponse>(access.Error);
        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var availability = EnsureOpen(access.Value.Schedule, now);
        if (availability.IsFailure) return Result.Failure<WorkspaceCheckpointLinkResponse>(availability.Error);

        var link = await context.SubmissionLinks.Include(item => item.Submission)
            .FirstOrDefaultAsync(item => item.Id == linkId && item.Submission.TeamId == teamId &&
                item.Submission.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
        if (link is null) return NotFound<WorkspaceCheckpointLinkResponse>();
        if (link.SubmittedById != userId) return Denied<WorkspaceCheckpointLinkResponse>("You can only update links you submitted.");
        if (await context.SubmissionLinks.AnyAsync(item => item.Id != linkId && item.Submission.TeamId == teamId &&
                item.Submission.CheckpointId == access.Value.Checkpoint.Id && item.Url.ToLower() == normalizedUrl.ToLower(), cancellationToken))
            return Invalid<WorkspaceCheckpointLinkResponse>("This URL has already been submitted for the checkpoint.");

        link.Name = request.Name.Trim();
        link.Url = normalizedUrl;
        link.UpdatedAt = now;
        link.UpdatedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(link, access.Value.UserName));
    }

    public async Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid linkId,
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsStudent(role)) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "Only team students can delete submitted links.");
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure(access.Error);
        var now = EnsureUtc(dateTimeProvider.UtcNow);
        var availability = EnsureOpen(access.Value.Schedule, now);
        if (availability.IsFailure) return availability;

        var link = await context.SubmissionLinks.Include(item => item.Submission)
            .FirstOrDefaultAsync(item => item.Id == linkId && item.Submission.TeamId == teamId &&
                item.Submission.CheckpointId == access.Value.Checkpoint.Id, cancellationToken);
        if (link is null) return Result.Failure(ErrorCodes.CommonNotFoundError, "Submitted link was not found.");
        if (link.SubmittedById != userId) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You can only delete links you submitted.");
        link.IsDeleted = true;
        link.DeletedAt = now;
        link.DeletedBy = userId;
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<Access>> ResolveAccessAsync(Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken)
    {
        var team = await context.Teams.AsNoTracking()
            .Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return Result.Failure<Access>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var member = team.TeamMembers.FirstOrDefault(item => item.CountsTowardActiveTeam &&
            item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && item.ClassStudent.Student.UserId == userId);
        if (!IsStudent(role) || member is null) return Result.Failure<Access>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var checkpoint = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item =>
            item.CourseId == team.Class.CourseId && item.ClassId == null && item.CheckpointNumber == checkpointNumber &&
            item.Status != CheckpointStatus.Archived, cancellationToken);
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (checkpoint is null || project is null)
            return Result.Failure<Access>(ErrorCodes.WorkspaceNotFound, "The checkpoint workspace was not found.");
        var schedule = await context.ClassCheckpointSchedules.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ClassId == team.ClassId && item.CheckpointId == checkpoint.Id, cancellationToken);
        return Result.Success(new Access(checkpoint, project, schedule, member.ClassStudent.Student.FullName));
    }

    private static Result EnsureOpen(ClassCheckpointSchedule? schedule, DateTime now)
    {
        if (schedule is not null && now >= schedule.StartDateUtc && now <= schedule.EndDateUtc) return Result.Success();
        var message = schedule is null
            ? "This checkpoint has not been scheduled for your class."
            : now < schedule.StartDateUtc
                ? $"This checkpoint opens at {schedule.StartDateUtc:O}."
                : $"This checkpoint closed at {schedule.EndDateUtc:O}.";
        return Result.Failure(ErrorCodes.WorkspaceCheckpointNotOpen, message);
    }

    private static WorkspaceCheckpointLinkResponse Map(SubmissionLink link, string userName) => new()
    {
        Id = link.Id,
        VersionNumber = link.VersionNumber,
        Name = link.Name,
        Url = link.Url,
        SubmittedAt = link.SubmittedAt,
        SubmittedBy = new WorkspaceCheckpointUserResponse
        {
            Id = link.SubmittedById,
            Name = userName,
            Role = SystemRoles.Student
        }
    };

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
    private static bool IsStudent(string role) => string.Equals(role, SystemRoles.Student, StringComparison.OrdinalIgnoreCase);
    private static Result<T> Denied<T>(string message = "You do not have access to this team workspace.") => Result.Failure<T>(ErrorCodes.WorkspaceAccessDenied, message);
    private static Result<T> Invalid<T>(string message) => Result.Failure<T>(ErrorCodes.WorkspaceValidationError, message);
    private static Result<T> NotFound<T>() => Result.Failure<T>(ErrorCodes.CommonNotFoundError, "Submitted link was not found.");
    private sealed record Access(Checkpoint Checkpoint, Project Project, ClassCheckpointSchedule? Schedule, string UserName);
}
