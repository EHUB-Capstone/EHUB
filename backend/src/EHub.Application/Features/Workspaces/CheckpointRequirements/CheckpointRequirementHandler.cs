using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointAvailability;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointRequirements;

public sealed class CheckpointRequirementHandler(
    IApplicationDbContext context,
    IClassRealtimePublisher? realtimePublisher = null) : ICheckpointRequirementHandler
{
    private const int MaximumContentLength = 5_000;

    public async Task<Result<WorkspaceCheckpointSubmissionResponse>> UpdateAsync(
        Guid teamId, int checkpointNumber, UpdateWorkspaceCheckpointRequirementsRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!IsRole(role, SystemRoles.Student)) return Denied();

        var team = await context.Teams
            .Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null || !team.TeamMembers.Any(member => member.CountsTowardActiveTeam &&
                member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active &&
                member.ClassStudent.Student.UserId == userId)) return Denied();

        var checkpoint = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item =>
            item.CourseId == team.Class.CourseId && item.ClassId == null &&
            item.CheckpointNumber == checkpointNumber && item.Status != CheckpointStatus.Archived, cancellationToken);
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (checkpoint is null || project is null)
            return Result.Failure<WorkspaceCheckpointSubmissionResponse>(ErrorCodes.WorkspaceNotFound, "The checkpoint workspace was not found.");

        var availability = await ResolveAvailabilityAsync(team.Id, team.ClassId, checkpoint, cancellationToken);
        if (!availability.CanSubmit)
            return Result.Failure<WorkspaceCheckpointSubmissionResponse>(ErrorCodes.WorkspaceValidationError, availability.Reason ?? "This checkpoint is not open for submission.");

        var requiredLabels = DeserializeRequirements(checkpoint.RequirementsJson);
        var normalized = NormalizeAndValidate(request?.Contents, requiredLabels.Count);
        if (normalized.IsFailure) return Result.Failure<WorkspaceCheckpointSubmissionResponse>(normalized.Error);

        var submission = await context.Submissions.Include(item => item.RequirementContents)
            .OrderByDescending(item => item.VersionNumber).ThenByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(item => item.TeamId == teamId && item.CheckpointId == checkpoint.Id, cancellationToken);
        var now = DateTime.UtcNow;
        if (submission is null)
        {
            submission = new Submission
            {
                ProjectId = project.Id, TeamId = teamId, CheckpointId = checkpoint.Id, Title = checkpoint.Name,
                Status = SubmissionStatus.Draft, VersionNumber = 1, CreatedAt = now, CreatedBy = userId
            };
            context.Submissions.Add(submission);
        }
        else
        {
            submission.UpdatedAt = now;
            submission.UpdatedBy = userId;
        }

        var existingByIndex = submission.RequirementContents.ToDictionary(item => item.RequirementIndex);
        var submittedIndexes = normalized.Value.Select(item => item.Index).ToHashSet();
        foreach (var stale in submission.RequirementContents.Where(item => !submittedIndexes.Contains(item.RequirementIndex)))
        {
            context.SubmissionRequirementContents.Remove(stale);
        }
        foreach (var item in normalized.Value)
        {
            if (existingByIndex.TryGetValue(item.Index, out var existing))
            {
                existing.Content = item.Content;
                existing.UpdatedAt = now;
                existing.UpdatedBy = userId;
            }
            else
            {
                submission.RequirementContents.Add(new SubmissionRequirementContent
                {
                    RequirementIndex = item.Index, Content = item.Content, CreatedAt = now, CreatedBy = userId
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        if (realtimePublisher is not null)
        {
            await realtimePublisher.PublishCheckpointRequirementsUpdatedAsync(
                await RecipientUserIdsAsync(team, cancellationToken), teamId, checkpointNumber, cancellationToken);
        }
        return Result.Success(new WorkspaceCheckpointSubmissionResponse
        {
            CheckpointNumber = checkpointNumber,
            Status = submission.Status.ToString(),
            RequirementContents = submission.RequirementContents.Where(item => submittedIndexes.Contains(item.RequirementIndex)).OrderBy(item => item.RequirementIndex)
                .Select(item => new WorkspaceCheckpointRequirementContentResponse { Index = item.RequirementIndex, Content = item.Content }).ToArray()
        });
    }

    private static Result<IReadOnlyCollection<NormalizedContent>> NormalizeAndValidate(
        IReadOnlyCollection<WorkspaceCheckpointRequirementContentInput>? contents, int requirementCount)
    {
        var values = contents ?? Array.Empty<WorkspaceCheckpointRequirementContentInput>();
        if (values.Count != requirementCount || values.Select(item => item.Index).Distinct().Count() != requirementCount ||
            values.Any(item => item.Index < 0 || item.Index >= requirementCount))
            return Result.Failure<IReadOnlyCollection<NormalizedContent>>(ErrorCodes.WorkspaceValidationError, "Provide exactly one value for every checkpoint requirement.");

        var normalized = values.Select(item => new NormalizedContent(item.Index, item.Content?.Trim() ?? string.Empty)).ToArray();
        return normalized.Any(item => item.Content.Length > MaximumContentLength)
            ? Result.Failure<IReadOnlyCollection<NormalizedContent>>(ErrorCodes.WorkspaceValidationError, $"Each requirement answer must not exceed {MaximumContentLength} characters.")
            : Result.Success<IReadOnlyCollection<NormalizedContent>>(normalized);
    }

    private static IReadOnlyCollection<string> DeserializeRequirements(string? requirementsJson)
    {
        try { return JsonSerializer.Deserialize<string[]>(requirementsJson ?? "[]") ?? Array.Empty<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);

    private async Task<CheckpointAvailabilityResult> ResolveAvailabilityAsync(Guid teamId, Guid classId, Checkpoint checkpoint, CancellationToken cancellationToken)
    {
        var schedule = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item =>
            item.ClassId == classId && item.CheckpointNumber == checkpoint.CheckpointNumber, cancellationToken);
        var previousCheckpoint = await context.Checkpoints.AsNoTracking().Where(item =>
                item.CourseId == checkpoint.CourseId && item.ClassId == null && item.CheckpointNumber < checkpoint.CheckpointNumber)
            .OrderByDescending(item => item.CheckpointNumber).FirstOrDefaultAsync(cancellationToken);
        var previousCompleted = previousCheckpoint is null || await context.Submissions.AsNoTracking().AnyAsync(item =>
            item.TeamId == teamId && item.CheckpointId == previousCheckpoint.Id && item.Status == SubmissionStatus.Submitted, cancellationToken);
        return CheckpointAvailabilityRules.Evaluate(schedule, previousCompleted, DateTime.UtcNow);
    }

    private async Task<IReadOnlyCollection<Guid>> RecipientUserIdsAsync(Team team, CancellationToken cancellationToken)
    {
        var administratorIds = await context.Users.AsNoTracking()
            .Where(user => user.UserRoles.Any(link => link.Role.Name == SystemRoles.Admin))
            .Select(user => user.Id)
            .ToArrayAsync(cancellationToken);
        return administratorIds
            .Concat(team.TeamMembers
                .Where(member => member.CountsTowardActiveTeam && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active)
                .Select(member => member.ClassStudent.Student.UserId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value))
            .Concat(team.MentorAssignments
                .Where(item => item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
                .Select(item => item.MentorProfile.UserId))
            .Concat(team.Class.ClassLecturers.Select(item => item.LecturerId))
            .Append(team.Class.PrimaryLecturerId ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
    }
    private static Result<WorkspaceCheckpointSubmissionResponse> Denied() => Result.Failure<WorkspaceCheckpointSubmissionResponse>(ErrorCodes.WorkspaceAccessDenied, "Only active team students can update checkpoint requirements.");
    private sealed record NormalizedContent(int Index, string Content);
}
