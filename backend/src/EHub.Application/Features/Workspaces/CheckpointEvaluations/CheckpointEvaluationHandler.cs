using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointEvaluations;

public sealed class CheckpointEvaluationHandler(
    IApplicationDbContext context,
    IClassRealtimePublisher? realtimePublisher = null) : ICheckpointEvaluationHandler
{
    private const int MaximumOverallFeedbackLength = 2_000;
    private const int MaximumCriterionCommentLength = 1_000;

    public async Task<Result<WorkspaceCheckpointEvaluationSummaryResponse>> GetSummaryAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var evaluationContext = await LoadContextAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (evaluationContext.IsFailure) return Result.Failure<WorkspaceCheckpointEvaluationSummaryResponse>(evaluationContext.Error);

        var item = evaluationContext.Value;
        var evaluations = await EvaluationQuery()
            .Where(evaluation => evaluation.ProjectId == item.Project.Id && evaluation.RubricId == item.Rubric.Id)
            .OrderByDescending(evaluation => evaluation.UpdatedAt ?? evaluation.CreatedAt)
            .ToListAsync(cancellationToken);
        // Older grades may still reference a particular submission version. Keep one current
        // grade per lecturer/checkpoint, preferring an official grade over a legacy draft.
        var currentEvaluations = evaluations
            .GroupBy(evaluation => evaluation.EvaluatorId)
            .Select(group => group
                .OrderByDescending(evaluation => evaluation.Status is EvaluationStatus.Submitted or EvaluationStatus.Published)
                .ThenByDescending(evaluation => evaluation.UpdatedAt ?? evaluation.CreatedAt)
                .ThenByDescending(evaluation => evaluation.Id)
                .First())
            .ToList();
        var visibleEvaluations = CanGrade(role, item.Team, userId)
            ? currentEvaluations
            : currentEvaluations.Where(evaluation => evaluation.Status is EvaluationStatus.Submitted or EvaluationStatus.Published).ToList();
        var history = visibleEvaluations
            .SelectMany(evaluation => evaluation.Histories)
            .OrderByDescending(entry => entry.ChangedAt)
            .ToArray();
        var submitted = visibleEvaluations.Where(evaluation => evaluation.Status is EvaluationStatus.Submitted or EvaluationStatus.Published).ToArray();

        return Result.Success(new WorkspaceCheckpointEvaluationSummaryResponse
        {
            Checkpoint = ToCheckpointResponse(item.Checkpoint, item.Rubric),
            Evaluations = visibleEvaluations.Select(ToEvaluationResponse).ToArray(),
            History = history.Select(ToHistoryResponse).ToArray(),
            Summary = new WorkspaceCheckpointEvaluationAggregateResponse
            {
                EvaluationCount = visibleEvaluations.Count,
                SubmittedCount = submitted.Length,
                AverageScore = submitted.Length == 0 ? 0 : Math.Round(submitted.Average(evaluation => evaluation.TotalScore), 2)
            }
        });
    }

    public async Task<Result<WorkspaceCheckpointEvaluationResponse>> SaveAsync(
        Guid teamId, int checkpointNumber, SaveWorkspaceCheckpointEvaluationRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var evaluationContext = await LoadContextAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (evaluationContext.IsFailure) return Result.Failure<WorkspaceCheckpointEvaluationResponse>(evaluationContext.Error);
        var item = evaluationContext.Value;
        if (!CanGrade(role, item.Team, userId)) return Denied<WorkspaceCheckpointEvaluationResponse>("Only the assigned lecturer can grade this checkpoint.");

        var existing = await EvaluationQuery(tracking: true)
            .Where(evaluation =>
                evaluation.ProjectId == item.Project.Id &&
                evaluation.RubricId == item.Rubric.Id &&
                evaluation.EvaluatorId == userId)
            .OrderByDescending(evaluation => evaluation.Status == EvaluationStatus.Submitted || evaluation.Status == EvaluationStatus.Published)
            .ThenByDescending(evaluation => evaluation.UpdatedAt ?? evaluation.CreatedAt)
            .ThenByDescending(evaluation => evaluation.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            var updated = await SaveExistingAsync(existing, item, request, userId, cancellationToken);
            if (updated.IsSuccess) await PublishUpdatedAsync(item.Team, checkpointNumber, cancellationToken);
            return updated;
        }

        var validation = ValidateRequest(request, item.Rubric.Criteria);
        if (validation.IsFailure) return Result.Failure<WorkspaceCheckpointEvaluationResponse>(validation.Error);

        var now = DateTime.UtcNow;
        var evaluation = new Evaluation
        {
            ProjectId = item.Project.Id,
            RubricId = item.Rubric.Id,
            EvaluatorId = userId,
            EvaluatorRole = EvaluatorRole.Lecturer,
            Status = validation.Value.Status,
            OverallFeedback = validation.Value.OverallFeedback,
            TotalScore = validation.Value.TotalScore,
            MaxTotalScore = 10,
            CreatedAt = now,
            CreatedBy = userId,
            SubmittedAt = validation.Value.Status == EvaluationStatus.Submitted ? now : null,
            PublishedAt = validation.Value.Status == EvaluationStatus.Submitted ? now : null
        };
        AddOrUpdateDetails(evaluation, item.Rubric.Criteria, validation.Value.Scores, userId, now);
        AddHistory(evaluation, validation.Value.Status == EvaluationStatus.Submitted
            ? EvaluationHistoryAction.Submitted
            : EvaluationHistoryAction.Created, userId, now);
        context.Evaluations.Add(evaluation);
        await context.SaveChangesAsync(cancellationToken);
        var saved = await EvaluationQuery().SingleAsync(item => item.Id == evaluation.Id, cancellationToken);
        await PublishUpdatedAsync(item.Team, checkpointNumber, cancellationToken);
        return Result.Success(ToEvaluationResponse(saved));
    }

    public async Task<Result<WorkspaceCheckpointEvaluationResponse>> UpdateAsync(
        Guid evaluationId, SaveWorkspaceCheckpointEvaluationRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluationQuery(tracking: true).FirstOrDefaultAsync(item => item.Id == evaluationId, cancellationToken);
        if (evaluation is null) return Result.Failure<WorkspaceCheckpointEvaluationResponse>(ErrorCodes.CommonNotFoundError, "Evaluation was not found.");
        if (evaluation.EvaluatorId != userId || !IsRole(role, SystemRoles.Lecturer))
            return Denied<WorkspaceCheckpointEvaluationResponse>("You can only update your own checkpoint evaluation.");

        var teamId = evaluation.Project.TeamId;
        var checkpointNumber = evaluation.Rubric.Checkpoint?.CheckpointNumber;
        if (!checkpointNumber.HasValue)
            return Result.Failure<WorkspaceCheckpointEvaluationResponse>(ErrorCodes.WorkspaceNotFound, "The evaluation rubric is not linked to a checkpoint.");
        var evaluationContext = await LoadContextAsync(teamId, checkpointNumber.Value, userId, role, cancellationToken);
        if (evaluationContext.IsFailure) return Result.Failure<WorkspaceCheckpointEvaluationResponse>(evaluationContext.Error);
        if (evaluationContext.Value.Rubric.Id != evaluation.RubricId)
            return Result.Failure<WorkspaceCheckpointEvaluationResponse>(ErrorCodes.WorkspaceValidationError, "This evaluation uses an outdated rubric. Create a new evaluation for the active rubric.");
        var updated = await SaveExistingAsync(evaluation, evaluationContext.Value, request, userId, cancellationToken);
        if (updated.IsSuccess) await PublishUpdatedAsync(evaluationContext.Value.Team, checkpointNumber.Value, cancellationToken);
        return updated;
    }

    private async Task<Result<WorkspaceCheckpointEvaluationResponse>> SaveExistingAsync(
        Evaluation evaluation, EvaluationContext evaluationContext, SaveWorkspaceCheckpointEvaluationRequest request,
        Guid userId, CancellationToken cancellationToken)
    {
        if (!CanGrade(SystemRoles.Lecturer, evaluationContext.Team, userId))
            return Denied<WorkspaceCheckpointEvaluationResponse>("Only the assigned lecturer can grade this checkpoint.");
        var validation = ValidateRequest(request, evaluationContext.Rubric.Criteria);
        if (validation.IsFailure) return Result.Failure<WorkspaceCheckpointEvaluationResponse>(validation.Error);

        var now = DateTime.UtcNow;
        var becameSubmitted = evaluation.Status != EvaluationStatus.Submitted && validation.Value.Status == EvaluationStatus.Submitted;
        evaluation.Status = validation.Value.Status;
        // Legacy evaluations can retain the ID of an older file version. A checkpoint grade
        // must survive later submissions, so detach it when the lecturer next saves it.
        evaluation.SubmissionId = null;
        evaluation.OverallFeedback = validation.Value.OverallFeedback;
        evaluation.TotalScore = validation.Value.TotalScore;
        evaluation.MaxTotalScore = 10;
        evaluation.UpdatedAt = now;
        evaluation.UpdatedBy = userId;
        if (becameSubmitted)
        {
            evaluation.SubmittedAt = now;
            evaluation.PublishedAt = now;
        }
        AddOrUpdateDetails(evaluation, evaluationContext.Rubric.Criteria, validation.Value.Scores, userId, now);
        AddHistory(evaluation, becameSubmitted ? EvaluationHistoryAction.Submitted : EvaluationHistoryAction.Updated, userId, now);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToEvaluationResponse(evaluation));
    }

    private async Task<Result<EvaluationContext>> LoadContextAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken)
    {
        var team = await context.Teams
            .Include(item => item.Class).ThenInclude(item => item.Course)
            .Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null || !CanView(role, team, userId))
            return Denied<EvaluationContext>("You do not have access to this team checkpoint.");

        var checkpoint = await context.Checkpoints
            .Include(item => item.Rubrics).ThenInclude(item => item.Criteria)
            .FirstOrDefaultAsync(item => item.CourseId == team.Class.CourseId && item.ClassId == null &&
                item.CheckpointNumber == checkpointNumber && item.Status != CheckpointStatus.Archived, cancellationToken);
        if (checkpoint is null)
            return Result.Failure<EvaluationContext>(ErrorCodes.WorkspaceNotFound, "The checkpoint workspace was not found.");
        var rubric = checkpoint.Rubrics.FirstOrDefault(item => item.ClassId == null && item.Status == RubricStatus.Active);
        if (rubric is null || rubric.Criteria.Count == 0)
            return Result.Failure<EvaluationContext>(ErrorCodes.WorkspaceValidationError, "The administrator has not configured an active rubric for this checkpoint.");
        var project = await context.Projects.FirstOrDefaultAsync(item => item.TeamId == teamId, cancellationToken);
        if (project is null)
            return Result.Failure<EvaluationContext>(ErrorCodes.WorkspaceNotFound, "The team project was not found.");
        return Result.Success(new EvaluationContext(team, project, checkpoint, rubric));
    }

    private static Result<ValidatedSave> ValidateRequest(
        SaveWorkspaceCheckpointEvaluationRequest? request, ICollection<RubricCriterion> criteria)
    {
        var overallFeedback = request?.OverallFeedback?.Trim();
        if (overallFeedback?.Length > MaximumOverallFeedbackLength)
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, $"Overall feedback must not exceed {MaximumOverallFeedbackLength} characters.");
        if (!Enum.TryParse<EvaluationStatus>(request?.Status, true, out var status) || status is not (EvaluationStatus.Draft or EvaluationStatus.Submitted))
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, "Status must be DRAFT or SUBMITTED.");

        var inputs = request?.RubricScores ?? Array.Empty<WorkspaceCheckpointCriterionScoreInput>();
        if (inputs.GroupBy(item => item.CriterionKey.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, "A criterion can only be scored once.");
        var criteriaByKey = criteria.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        if (inputs.Any(item => string.IsNullOrWhiteSpace(item.CriterionKey) || !criteriaByKey.ContainsKey(item.CriterionKey.Trim())))
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, "One or more submitted criteria are no longer part of this checkpoint rubric.");
        if (inputs.Any(item => item.Comment?.Trim().Length > MaximumCriterionCommentLength))
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, $"Each criterion comment must not exceed {MaximumCriterionCommentLength} characters.");
        if (inputs.Any(item => item.Score is < 0 || (item.Score.HasValue && item.Score.Value > criteriaByKey[item.CriterionKey.Trim()].MaxScore)))
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, "Each score must be within the configured criterion range.");
        if (status == EvaluationStatus.Submitted && (inputs.Count != criteria.Count || inputs.Any(item => !item.Score.HasValue)))
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, "Score every configured criterion before submitting the evaluation.");

        var normalized = inputs.Where(item => item.Score.HasValue).Select(item => new ValidatedScore(
            item.CriterionKey.Trim(), item.Score!.Value, item.Comment?.Trim())).ToArray();
        var total = normalized.Sum(item => item.Score * criteriaByKey[item.CriterionKey].Weight / 100m);
        return Result.Success(new ValidatedSave(status, overallFeedback, Math.Round(total, 2), normalized));
    }

    private void AddOrUpdateDetails(Evaluation evaluation, ICollection<RubricCriterion> criteria,
        IReadOnlyCollection<ValidatedScore> scores, Guid userId, DateTime now)
    {
        var criterionByKey = criteria.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var detailsByCriterion = evaluation.Details.ToDictionary(item => item.RubricCriterionId);
        var scoreCriterionIds = scores.Select(item => criterionByKey[item.CriterionKey].Id).ToHashSet();
        foreach (var removed in evaluation.Details
                     .Where(item => criterionByKey.Values.Any(criterion => criterion.Id == item.RubricCriterionId) &&
                                    !scoreCriterionIds.Contains(item.RubricCriterionId))
                     .ToArray())
        {
            context.EvaluationDetails.Remove(removed);
        }
        foreach (var score in scores)
        {
            var criterion = criterionByKey[score.CriterionKey];
            if (detailsByCriterion.TryGetValue(criterion.Id, out var detail))
            {
                detail.Score = score.Score;
                detail.Comment = score.Comment;
                detail.UpdatedAt = now;
                detail.UpdatedBy = userId;
            }
            else
            {
                evaluation.Details.Add(new EvaluationDetail
                {
                    RubricCriterionId = criterion.Id,
                    Score = score.Score,
                    Comment = score.Comment,
                    CreatedAt = now,
                    CreatedBy = userId
                });
            }
        }
    }

    private void AddHistory(Evaluation evaluation, EvaluationHistoryAction action, Guid userId, DateTime now)
    {
        var nextVersion = evaluation.Histories.Count == 0 ? 1 : evaluation.Histories.Max(item => item.Version) + 1;
        var history = new EvaluationHistory
        {
            Version = nextVersion,
            Action = action,
            SnapshotJson = JsonSerializer.Serialize(new { evaluation.Status, evaluation.TotalScore, evaluation.OverallFeedback }),
            ChangedById = userId,
            ChangedAt = now,
            CreatedAt = now
        };
        evaluation.Histories.Add(history);
        // IDs are assigned before EF tracks the entity; mark the new history row explicitly.
        context.EvaluationHistories.Add(history);
    }

    private async Task PublishUpdatedAsync(Team team, int checkpointNumber, CancellationToken cancellationToken)
    {
        if (realtimePublisher is null) return;
        try
        {
            var administratorIds = await context.Users.AsNoTracking()
                .Where(user => user.UserRoles.Any(link => link.Role.Name == SystemRoles.Admin))
                .Select(user => user.Id)
                .ToArrayAsync(cancellationToken);
            var recipients = administratorIds
                .Concat(team.TeamMembers.Where(member => member.CountsTowardActiveTeam && member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active)
                    .Select(member => member.ClassStudent.Student.UserId).Where(id => id.HasValue).Select(id => id!.Value))
                .Concat(team.MentorAssignments.Where(item => item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
                    .Select(item => item.MentorProfile.UserId))
                .Concat(team.Class.ClassLecturers.Select(item => item.LecturerId))
                .Append(team.Class.PrimaryLecturerId ?? Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();
            await realtimePublisher.PublishCheckpointEvaluationUpdatedAsync(recipients, team.Id, checkpointNumber, cancellationToken);
        }
        catch
        {
            // A committed evaluation must not fail because its best-effort notification could not be delivered.
        }
    }

    private IQueryable<Evaluation> EvaluationQuery(bool tracking = false)
    {
        var query = tracking ? context.Evaluations.AsQueryable() : context.Evaluations.AsNoTracking();
        return query
            .Include(item => item.Project).ThenInclude(item => item.Team)
            .Include(item => item.Rubric).ThenInclude(item => item.Checkpoint)
            .Include(item => item.Evaluator)
            .Include(item => item.Details).ThenInclude(item => item.RubricCriterion)
            .Include(item => item.Histories).ThenInclude(item => item.ChangedBy);
    }

    private static WorkspaceCheckpointEvaluationConfigResponse ToCheckpointResponse(Checkpoint checkpoint, Rubric rubric) => new()
    {
        Number = checkpoint.CheckpointNumber,
        Title = checkpoint.Name,
        ShortDescription = checkpoint.Description,
        Rubrics = rubric.Criteria.OrderBy(item => item.DisplayOrder).Select(item => new WorkspaceCheckpointEvaluationCriterionResponse
        {
            Key = item.Key,
            Label = item.Name,
            Description = item.Description,
            Weight = item.Weight,
            MaxScore = item.MaxScore,
            Levels = DeserializeLevels(item.LevelsJson)
        }).ToArray()
    };

    private static WorkspaceCheckpointEvaluationResponse ToEvaluationResponse(Evaluation evaluation) => new()
    {
        Id = evaluation.Id,
        LecturerId = ToUserResponse(evaluation.Evaluator),
        EvaluatorRole = evaluation.EvaluatorRole.ToString(),
        Status = evaluation.Status.ToString().ToUpperInvariant(),
        CheckpointTotal = evaluation.TotalScore,
        OverallFeedback = evaluation.OverallFeedback,
        UpdatedAt = evaluation.UpdatedAt ?? evaluation.CreatedAt,
        RubricScores = evaluation.Details.OrderBy(item => item.RubricCriterion.DisplayOrder).Select(item => new WorkspaceCheckpointCriterionScoreResponse
        {
            CriterionKey = item.RubricCriterion.Key,
            CriterionName = item.RubricCriterion.Name,
            Score = item.Score,
            Comment = item.Comment
        }).ToArray()
    };

    private static WorkspaceCheckpointEvaluationHistoryResponse ToHistoryResponse(EvaluationHistory history) => new()
    {
        Id = history.Id,
        Action = history.Action.ToString().ToUpperInvariant(),
        Version = history.Version,
        ChangedBy = ToUserResponse(history.ChangedBy),
        CreatedAt = history.ChangedAt,
        Note = history.Note
    };

    private static WorkspaceCheckpointUserResponse ToUserResponse(User? user) => user is null ? new WorkspaceCheckpointUserResponse { Name = "System" } : new()
    {
        Id = user.Id,
        Name = user.FullName,
        AvatarUrl = user.AvatarUrl
    };

    private static IReadOnlyCollection<object> DeserializeLevels(string? value)
    {
        try { return JsonSerializer.Deserialize<object[]>(value ?? "[]") ?? Array.Empty<object>(); }
        catch (JsonException) { return Array.Empty<object>(); }
    }

    private static bool CanView(string role, Team team, Guid userId) =>
        IsRole(role, SystemRoles.Admin) ||
        (IsRole(role, SystemRoles.Lecturer) && (team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(item => item.LecturerId == userId))) ||
        (IsRole(role, SystemRoles.Mentor) && team.MentorAssignments.Any(item => item.Status == MentorAssignmentStatus.Active && item.EndedAt == null && item.MentorProfile.UserId == userId)) ||
        (IsRole(role, SystemRoles.Student) && team.TeamMembers.Any(item => item.CountsTowardActiveTeam && item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active && item.ClassStudent.Student.UserId == userId));

    private static bool CanGrade(string role, Team team, Guid userId) =>
        IsRole(role, SystemRoles.Lecturer) && (team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(item => item.LecturerId == userId));

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
    private static Result<T> Denied<T>(string message) => Result.Failure<T>(ErrorCodes.WorkspaceAccessDenied, message);

    private sealed record EvaluationContext(Team Team, Project Project, Checkpoint Checkpoint, Rubric Rubric);
    private sealed record ValidatedScore(string CriterionKey, decimal Score, string? Comment);
    private sealed record ValidatedSave(EvaluationStatus Status, string? OverallFeedback, decimal TotalScore, IReadOnlyCollection<ValidatedScore> Scores);
}
