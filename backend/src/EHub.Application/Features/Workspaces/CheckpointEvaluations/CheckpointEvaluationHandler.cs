using System.Text.Json;
using System.Globalization;
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
    private const int MaximumBulkPublicationCount = 200;
    private const int MaximumGradingBatchTeamCount = 200;

    public async Task<Result<EvaluationGradingBatchResponse>> GetGradingBatchAsync(
        EvaluationGradingBatchRequest request, Guid userId, string role,
        CancellationToken cancellationToken = default)
    {
        var requestedTeamIds = request?.TeamIds ?? Array.Empty<Guid>();
        if (requestedTeamIds.Count > MaximumGradingBatchTeamCount || requestedTeamIds.Any(id => id == Guid.Empty))
            return Result.Failure<EvaluationGradingBatchResponse>(
                ErrorCodes.WorkspaceValidationError,
                $"Select up to {MaximumGradingBatchTeamCount} valid teams at once.");
        var teamIds = requestedTeamIds.Distinct().ToArray();
        if (teamIds.Length == 0)
            return Result.Success(new EvaluationGradingBatchResponse());

        var teamQuery = context.Teams
            .AsNoTracking()
            .Where(team => teamIds.Contains(team.Id) && team.Status == TeamStatus.Active);
        if (IsRole(role, SystemRoles.Lecturer))
        {
            teamQuery = teamQuery.Where(team =>
                team.Class.PrimaryLecturerId == userId ||
                team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId));
        }
        else if (IsRole(role, SystemRoles.Mentor))
        {
            teamQuery = teamQuery.Where(team => team.MentorAssignments.Any(assignment =>
                assignment.MentorProfile.UserId == userId &&
                assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null));
        }
        else if (IsRole(role, SystemRoles.Student))
        {
            teamQuery = teamQuery.Where(team => team.TeamMembers.Any(member =>
                member.CountsTowardActiveTeam &&
                member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active &&
                member.ClassStudent.Student.UserId == userId));
        }
        else if (!IsRole(role, SystemRoles.Admin))
        {
            return Denied<EvaluationGradingBatchResponse>("You do not have access to evaluation grading data.");
        }

        var teams = await teamQuery
            .Include(team => team.Class).ThenInclude(targetClass => targetClass.Course)
            .Include(team => team.Class).ThenInclude(targetClass => targetClass.ClassLecturers)
            .Include(team => team.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
            .Include(team => team.MentorAssignments).ThenInclude(assignment => assignment.MentorProfile)
            .Include(team => team.Project)
            .Include(team => team.ApprovedProposals)
            .OrderBy(team => team.Class.ClassCode)
            .ThenBy(team => team.TeamName)
            .ToArrayAsync(cancellationToken);
        if (teams.Length != teamIds.Length)
            return Denied<EvaluationGradingBatchResponse>("One or more requested teams are unavailable or outside your access scope.");

        var courseIds = teams.Select(team => team.Class.CourseId).Distinct().ToArray();
        var checkpoints = await context.Checkpoints
            .AsNoTracking()
            .Where(checkpoint => checkpoint.CourseId.HasValue && courseIds.Contains(checkpoint.CourseId.Value) &&
                                 checkpoint.ClassId == null && checkpoint.Status != CheckpointStatus.Archived)
            .Include(checkpoint => checkpoint.Rubrics.Where(rubric =>
                rubric.ClassId == null && rubric.Status == RubricStatus.Active))
            .ThenInclude(rubric => rubric.Criteria)
            .OrderBy(checkpoint => checkpoint.CheckpointNumber)
            .ToArrayAsync(cancellationToken);
        var assessments = await context.Rubrics
            .AsNoTracking()
            .Where(rubric => rubric.CourseId.HasValue && courseIds.Contains(rubric.CourseId.Value) &&
                             rubric.ClassId == null && rubric.CheckpointId == null &&
                             rubric.Status == RubricStatus.Active)
            .OrderBy(rubric => rubric.Name)
            .ToArrayAsync(cancellationToken);

        var checkpointRubrics = checkpoints
            .Select(checkpoint => checkpoint.Rubrics.FirstOrDefault(rubric => rubric.Criteria.Count > 0))
            .Where(rubric => rubric is not null)
            .Cast<Rubric>()
            .ToArray();
        var rubricIds = checkpointRubrics.Select(rubric => rubric.Id)
            .Concat(assessments.Select(assessment => assessment.Id))
            .Distinct()
            .ToArray();
        var projectIds = teams.Where(team => team.Project is not null)
            .Select(team => team.Project!.Id)
            .Distinct()
            .ToArray();
        var evaluations = projectIds.Length == 0 || rubricIds.Length == 0
            ? Array.Empty<Evaluation>()
            : await context.Evaluations
                .AsNoTracking()
                .Where(evaluation => projectIds.Contains(evaluation.ProjectId) && rubricIds.Contains(evaluation.RubricId))
                .Include(evaluation => evaluation.Evaluator)
                .Include(evaluation => evaluation.Details).ThenInclude(detail => detail.RubricCriterion)
                .Include(evaluation => evaluation.MemberScores)
                .OrderByDescending(evaluation => evaluation.UpdatedAt ?? evaluation.CreatedAt)
                .ToArrayAsync(cancellationToken);
        var evaluationsByProjectAndRubric = evaluations.ToLookup(evaluation => (evaluation.ProjectId, evaluation.RubricId));

        var responseTeams = teams.Select(team =>
        {
            var canGrade = CanGrade(role, team, userId);
            var teamCheckpoints = checkpoints
                .Where(checkpoint => checkpoint.CourseId == team.Class.CourseId)
                .Select(checkpoint => (Checkpoint: checkpoint, Rubric: checkpoint.Rubrics.FirstOrDefault(rubric => rubric.Criteria.Count > 0)))
                .Where(item => item.Rubric is not null)
                .Select(item =>
                {
                    var candidates = team.Project is null
                        ? Array.Empty<Evaluation>()
                        : evaluationsByProjectAndRubric[(team.Project.Id, item.Rubric!.Id)].ToArray();
                    var current = candidates
                        .GroupBy(evaluation => evaluation.EvaluatorId)
                        .Select(group => group
                            .OrderByDescending(evaluation => evaluation.Status is EvaluationStatus.Submitted or EvaluationStatus.Published)
                            .ThenByDescending(evaluation => evaluation.UpdatedAt ?? evaluation.CreatedAt)
                            .ThenByDescending(evaluation => evaluation.Id)
                            .First())
                        .Where(evaluation => canGrade || evaluation.Status is EvaluationStatus.Submitted or EvaluationStatus.Published)
                        .Select(evaluation => ToEvaluationResponse(evaluation, team, userId, role))
                        .ToArray();
                    return new EvaluationGradingCheckpointResponse
                    {
                        Checkpoint = ToCheckpointResponse(item.Checkpoint, item.Rubric!, team, userId, role),
                        Evaluations = current
                    };
                })
                .ToArray();
            var teamAssessments = assessments
                .Where(assessment => assessment.CourseId == team.Class.CourseId)
                .Select(assessment =>
                {
                    var evaluation = team.Project is null
                        ? null
                        : evaluationsByProjectAndRubric[(team.Project.Id, assessment.Id)]
                            .Where(item => canGrade || item.Status is EvaluationStatus.Submitted or EvaluationStatus.Published)
                            .OrderByDescending(item => item.Status is EvaluationStatus.Submitted or EvaluationStatus.Published)
                            .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                            .FirstOrDefault();
                    return ToCourseAssessmentResponse(assessment, evaluation, team, userId, role);
                })
                .ToArray();
            var proposal = team.ApprovedProposals.OrderByDescending(item => item.CreatedAt).FirstOrDefault();
            var semesterGroups = ActiveMembers(team)
                .Select(member => member.ClassStudent.SemesterGroupName?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToArray();

            return new EvaluationGradingTeamResponse
            {
                TeamId = team.Id,
                TeamCode = team.TeamCode,
                ProjectName = team.Project?.Name ?? proposal?.ProjectName ?? proposal?.TeamName,
                ProjectDescription = team.Project?.Description ?? proposal?.Description,
                SemesterGroupName = string.Join(", ", semesterGroups),
                Members = team.TeamMembers
                    .Where(member => member.CountsTowardActiveTeam &&
                        (EvaluationVisibilityRules.IsInternalViewer(role) ||
                         (IsRole(role, SystemRoles.Student) && member.ClassStudent.Student.UserId == userId)))
                    .OrderBy(member => member.ClassStudent.Student.FullName)
                    .ThenBy(member => member.StudentId)
                    .Select(member => new EvaluationGradingTeamMemberResponse
                    {
                        StudentId = member.StudentId,
                        UserId = member.ClassStudent.Student.UserId,
                        FullName = member.ClassStudent.Student.FullName,
                        RollNumber = member.ClassStudent.Student.RollNumber ?? string.Empty,
                        MajorCode = member.ClassStudent.MajorCodeAtEnrollment,
                        RoleInTeam = member.RoleInTeam.ToString()
                    })
                    .ToArray(),
                Checkpoints = teamCheckpoints,
                Assessments = teamAssessments
            };
        }).ToArray();

        return Result.Success(new EvaluationGradingBatchResponse { Teams = responseTeams });
    }

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
        var canGrade = CanGrade(role, item.Team, userId);
        var visibleEvaluations = canGrade
            ? currentEvaluations
            : currentEvaluations.Where(evaluation => evaluation.Status is EvaluationStatus.Submitted or EvaluationStatus.Published).ToList();
        var visibleHistoryEvaluations = canGrade
            ? evaluations
            : evaluations.Where(evaluation => evaluation.Status is EvaluationStatus.Submitted or EvaluationStatus.Published).ToList();
        var history = visibleHistoryEvaluations
            .SelectMany(evaluation => evaluation.Histories)
            .OrderBy(entry => entry.ChangedAt)
            .ThenBy(entry => entry.EvaluationId)
            .ThenBy(entry => entry.Version)
            .ToArray();
        var submitted = visibleEvaluations.Where(evaluation => evaluation.Status is EvaluationStatus.Submitted or EvaluationStatus.Published).ToArray();
        var scoreVisibleEvaluations = submitted
            .Where(evaluation => EvaluationVisibilityRules.CanViewTeamScore(role, evaluation.Status))
            .ToArray();

        return Result.Success(new WorkspaceCheckpointEvaluationSummaryResponse
        {
            Checkpoint = ToCheckpointResponse(item.Checkpoint, item.Rubric, item.Team, userId, role),
            Evaluations = visibleEvaluations
                .Select(evaluation => ToEvaluationResponse(evaluation, item.Team, userId, role))
                .ToArray(),
            History = EvaluationVisibilityRules.IsInternalViewer(role)
                ? ToHistoryResponses(history, item.Team, userId, role)
                : Array.Empty<WorkspaceCheckpointEvaluationHistoryResponse>(),
            Summary = new WorkspaceCheckpointEvaluationAggregateResponse
            {
                EvaluationCount = visibleEvaluations.Count,
                SubmittedCount = submitted.Length,
                AverageScore = scoreVisibleEvaluations.Length == 0
                    ? null
                    : Math.Round(scoreVisibleEvaluations.Average(evaluation => evaluation.TotalScore), 2)
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

        var validation = ValidateRequest(request, item.Rubric.Criteria, item.Team);
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
            PublishedAt = null
        };
        AddOrUpdateDetails(evaluation, item.Rubric.Criteria, validation.Value.Scores, userId, now);
        AddOrUpdateMemberScores(evaluation, validation.Value.MemberScoreOverrides, userId, now);
        AddHistory(evaluation, validation.Value.Status == EvaluationStatus.Submitted
            ? EvaluationHistoryAction.Submitted
            : EvaluationHistoryAction.Created, validation.Value.Scores,
            validation.Value.MemberScoreOverrides, item.Rubric, item.Team, userId, now);
        context.Evaluations.Add(evaluation);
        await context.SaveChangesAsync(cancellationToken);
        var saved = await EvaluationQuery().SingleAsync(item => item.Id == evaluation.Id, cancellationToken);
        await PublishUpdatedAsync(item.Team, checkpointNumber, cancellationToken);
        return Result.Success(ToEvaluationResponse(saved, item.Team, userId, role));
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

    public async Task<Result<WorkspaceEvaluationPublicationResponse>> PublishAsync(
        Guid evaluationId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluationQuery(tracking: true)
            .FirstOrDefaultAsync(item => item.Id == evaluationId, cancellationToken);
        if (evaluation is null)
            return Result.Failure<WorkspaceEvaluationPublicationResponse>(
                ErrorCodes.CommonNotFoundError, "Evaluation was not found.");

        var team = evaluation.Project.Team;
        if (!IsRole(role, SystemRoles.Lecturer) || evaluation.EvaluatorId != userId || !CanGrade(role, team, userId))
            return Denied<WorkspaceEvaluationPublicationResponse>(
                "Only the assigned lecturer who submitted this evaluation can publish it.");
        if (evaluation.Status == EvaluationStatus.Published && evaluation.PublishedAt.HasValue)
        {
            return Result.Success(new WorkspaceEvaluationPublicationResponse
            {
                Id = evaluation.Id,
                Status = EvaluationStatus.Published.ToString().ToUpperInvariant(),
                PublishedAt = evaluation.PublishedAt.Value,
            });
        }
        if (evaluation.Status != EvaluationStatus.Submitted)
            return Result.Failure<WorkspaceEvaluationPublicationResponse>(
                ErrorCodes.WorkspaceValidationError, "Only a submitted evaluation can be published.");

        var now = DateTime.UtcNow;
        evaluation.Status = EvaluationStatus.Published;
        evaluation.PublishedAt = now;
        evaluation.UpdatedAt = now;
        evaluation.UpdatedBy = userId;
        AddHistory(evaluation, EvaluationHistoryAction.Published,
            evaluation.Details.Select(item => new ValidatedScore(
                item.RubricCriterion.Key, item.Score, item.Comment)).ToArray(),
            evaluation.MemberScores.Select(item => new ValidatedMemberScore(item.StudentId, item.Score)).ToArray(),
            evaluation.Rubric, team, userId, now);
        await context.SaveChangesAsync(cancellationToken);

        var checkpointNumber = evaluation.Rubric.Checkpoint?.CheckpointNumber;
        if (checkpointNumber.HasValue)
            await PublishUpdatedAsync(team, checkpointNumber.Value, cancellationToken);

        return Result.Success(new WorkspaceEvaluationPublicationResponse
        {
            Id = evaluation.Id,
            Status = EvaluationStatus.Published.ToString().ToUpperInvariant(),
            PublishedAt = now,
        });
    }

    public async Task<Result<WorkspaceEvaluationUnpublicationResponse>> UnpublishAsync(
        Guid evaluationId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluationQuery(tracking: true)
            .FirstOrDefaultAsync(item => item.Id == evaluationId, cancellationToken);
        if (evaluation is null)
            return Result.Failure<WorkspaceEvaluationUnpublicationResponse>(
                ErrorCodes.CommonNotFoundError, "Evaluation was not found.");

        var team = evaluation.Project.Team;
        if (!IsRole(role, SystemRoles.Lecturer) || evaluation.EvaluatorId != userId || !CanGrade(role, team, userId))
            return Denied<WorkspaceEvaluationUnpublicationResponse>(
                "Only the assigned lecturer who published this evaluation can hide its scores.");
        if (evaluation.Status != EvaluationStatus.Published || !evaluation.PublishedAt.HasValue)
            return Result.Failure<WorkspaceEvaluationUnpublicationResponse>(
                ErrorCodes.WorkspaceValidationError, "Only a published evaluation can be hidden.");

        var now = DateTime.UtcNow;
        evaluation.Status = EvaluationStatus.Submitted;
        evaluation.PublishedAt = null;
        evaluation.UpdatedAt = now;
        evaluation.UpdatedBy = userId;
        AddHistory(evaluation, EvaluationHistoryAction.Unpublished,
            evaluation.Details.Select(item => new ValidatedScore(
                item.RubricCriterion.Key, item.Score, item.Comment)).ToArray(),
            evaluation.MemberScores.Select(item => new ValidatedMemberScore(item.StudentId, item.Score)).ToArray(),
            evaluation.Rubric, team, userId, now);
        await context.SaveChangesAsync(cancellationToken);

        var checkpointNumber = evaluation.Rubric.Checkpoint?.CheckpointNumber;
        if (checkpointNumber.HasValue)
            await PublishUpdatedAsync(team, checkpointNumber.Value, cancellationToken);

        return Result.Success(new WorkspaceEvaluationUnpublicationResponse
        {
            Id = evaluation.Id,
            Status = EvaluationStatus.Submitted.ToString().ToUpperInvariant(),
            UpdatedAt = now,
        });
    }

    public async Task<Result<BulkWorkspaceEvaluationPublicationResponse>> UpdatePublicationBatchAsync(
        BulkWorkspaceEvaluationPublicationRequest request, Guid userId, string role,
        CancellationToken cancellationToken = default)
    {
        var action = request?.Action?.Trim().ToUpperInvariant();
        if (action is not ("PUBLISH" or "UNPUBLISH"))
            return Result.Failure<BulkWorkspaceEvaluationPublicationResponse>(
                ErrorCodes.WorkspaceValidationError, "Action must be PUBLISH or UNPUBLISH.");

        var requestedIds = request?.EvaluationIds ?? Array.Empty<Guid>();
        if (requestedIds.Any(id => id == Guid.Empty))
            return Result.Failure<BulkWorkspaceEvaluationPublicationResponse>(
                ErrorCodes.WorkspaceValidationError, "Evaluation IDs must be valid.");
        var evaluationIds = requestedIds.Distinct().ToArray();
        if (evaluationIds.Length == 0 || evaluationIds.Length > MaximumBulkPublicationCount)
            return Result.Failure<BulkWorkspaceEvaluationPublicationResponse>(
                ErrorCodes.WorkspaceValidationError,
                $"Select between 1 and {MaximumBulkPublicationCount} evaluations.");

        var evaluations = await EvaluationQuery(tracking: true)
            .Where(item => evaluationIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        if (evaluations.Count != evaluationIds.Length)
            return Result.Failure<BulkWorkspaceEvaluationPublicationResponse>(
                ErrorCodes.CommonNotFoundError, "One or more evaluations were not found.");
        if (!IsRole(role, SystemRoles.Lecturer) || evaluations.Any(evaluation =>
                evaluation.EvaluatorId != userId || !CanGrade(role, evaluation.Project.Team, userId)))
            return Denied<BulkWorkspaceEvaluationPublicationResponse>(
                "You can only change publication for evaluations that you submitted in assigned classes.");

        var isPublish = action == "PUBLISH";
        if (evaluations.Any(evaluation => isPublish
                ? evaluation.Status is not (EvaluationStatus.Submitted or EvaluationStatus.Published)
                : evaluation.Status is not (EvaluationStatus.Published or EvaluationStatus.Submitted)))
            return Result.Failure<BulkWorkspaceEvaluationPublicationResponse>(
                ErrorCodes.WorkspaceValidationError,
                isPublish
                    ? "Only submitted evaluations can be published."
                    : "Only published evaluations can be hidden.");

        var now = DateTime.UtcNow;
        var changed = evaluations.Where(evaluation => isPublish
                ? evaluation.Status != EvaluationStatus.Published || !evaluation.PublishedAt.HasValue
                : evaluation.Status != EvaluationStatus.Submitted || evaluation.PublishedAt.HasValue)
            .ToArray();
        foreach (var evaluation in changed)
        {
            evaluation.Status = isPublish ? EvaluationStatus.Published : EvaluationStatus.Submitted;
            evaluation.PublishedAt = isPublish ? now : null;
            evaluation.UpdatedAt = now;
            evaluation.UpdatedBy = userId;
            AddHistory(evaluation,
                isPublish ? EvaluationHistoryAction.Published : EvaluationHistoryAction.Unpublished,
                evaluation.Details.Select(item => new ValidatedScore(
                    item.RubricCriterion.Key, item.Score, item.Comment)).ToArray(),
                evaluation.MemberScores.Select(item => new ValidatedMemberScore(item.StudentId, item.Score)).ToArray(),
                evaluation.Rubric, evaluation.Project.Team, userId, now);
        }

        if (changed.Length > 0)
            await context.SaveChangesAsync(cancellationToken);

        foreach (var evaluation in changed
                     .Where(item => item.Rubric.Checkpoint is not null)
                     .DistinctBy(item => (item.Project.TeamId, item.Rubric.Checkpoint!.CheckpointNumber)))
        {
            await PublishUpdatedAsync(
                evaluation.Project.Team, evaluation.Rubric.Checkpoint!.CheckpointNumber, cancellationToken);
        }

        return Result.Success(new BulkWorkspaceEvaluationPublicationResponse
        {
            Action = action,
            TargetStatus = (isPublish ? EvaluationStatus.Published : EvaluationStatus.Submitted)
                .ToString().ToUpperInvariant(),
            RequestedCount = evaluationIds.Length,
            ChangedCount = changed.Length,
            UnchangedCount = evaluationIds.Length - changed.Length,
        });
    }

    private async Task<Result<WorkspaceCheckpointEvaluationResponse>> SaveExistingAsync(
        Evaluation evaluation, EvaluationContext evaluationContext, SaveWorkspaceCheckpointEvaluationRequest request,
        Guid userId, CancellationToken cancellationToken)
    {
        if (!CanGrade(SystemRoles.Lecturer, evaluationContext.Team, userId))
            return Denied<WorkspaceCheckpointEvaluationResponse>("Only the assigned lecturer can grade this checkpoint.");
        var validation = ValidateRequest(request, evaluationContext.Rubric.Criteria, evaluationContext.Team);
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
        evaluation.PublishedAt = null;
        if (validation.Value.Status == EvaluationStatus.Submitted)
        {
            evaluation.SubmittedAt ??= now;
        }
        AddOrUpdateDetails(evaluation, evaluationContext.Rubric.Criteria, validation.Value.Scores, userId, now);
        AddOrUpdateMemberScores(evaluation, validation.Value.MemberScoreOverrides, userId, now);
        AddHistory(evaluation, becameSubmitted ? EvaluationHistoryAction.Submitted : EvaluationHistoryAction.Updated,
            validation.Value.Scores, validation.Value.MemberScoreOverrides,
            evaluationContext.Rubric, evaluationContext.Team, userId, now);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToEvaluationResponse(evaluation, evaluationContext.Team, userId, SystemRoles.Lecturer));
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
        SaveWorkspaceCheckpointEvaluationRequest? request, ICollection<RubricCriterion> criteria, Team team)
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
        var memberInputs = request?.MemberScoreOverrides ?? Array.Empty<WorkspaceCheckpointMemberScoreInput>();
        if (memberInputs.GroupBy(item => item.StudentId).Any(group => group.Count() > 1))
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, "A team member can only have one individual score override.");
        if (memberInputs.Any(item => item.Score is < 0 or > 10))
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, "Each individual member score must be between 0 and 10.");
        var activeStudentIds = team.TeamMembers
            .Where(item => item.CountsTowardActiveTeam && item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active)
            .Select(item => item.StudentId)
            .ToHashSet();
        if (memberInputs.Any(item => !activeStudentIds.Contains(item.StudentId)))
            return Result.Failure<ValidatedSave>(ErrorCodes.WorkspaceValidationError, "One or more individual scores belong to a student outside this team.");
        var memberScores = memberInputs.Select(item => new ValidatedMemberScore(item.StudentId, Math.Round(item.Score, 2))).ToArray();
        return Result.Success(new ValidatedSave(status, overallFeedback, Math.Round(total, 2), normalized, memberScores));
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

    private void AddOrUpdateMemberScores(Evaluation evaluation,
        IReadOnlyCollection<ValidatedMemberScore> scores, Guid userId, DateTime now)
    {
        var requestedByStudent = scores.ToDictionary(item => item.StudentId);
        foreach (var removed in evaluation.MemberScores
                     .Where(item => !requestedByStudent.ContainsKey(item.StudentId))
                     .ToArray())
        {
            evaluation.MemberScores.Remove(removed);
            context.EvaluationMemberScores.Remove(removed);
        }

        var existingByStudent = evaluation.MemberScores.ToDictionary(item => item.StudentId);
        foreach (var score in scores)
        {
            if (existingByStudent.TryGetValue(score.StudentId, out var existing))
            {
                existing.Score = score.Score;
                existing.UpdatedAt = now;
                existing.UpdatedBy = userId;
            }
            else
            {
                var memberScore = new EvaluationMemberScore
                {
                    StudentId = score.StudentId,
                    Score = score.Score,
                    CreatedAt = now,
                    CreatedBy = userId
                };
                evaluation.MemberScores.Add(memberScore);
                // IDs are assigned before EF tracks the entity, so explicitly mark new overrides as Added.
                context.EvaluationMemberScores.Add(memberScore);
            }
        }
    }

    private void AddHistory(Evaluation evaluation, EvaluationHistoryAction action,
        IReadOnlyCollection<ValidatedScore> rubricScores,
        IReadOnlyCollection<ValidatedMemberScore> memberScoreOverrides,
        Rubric rubric, Team team, Guid userId, DateTime now)
    {
        var nextVersion = evaluation.Histories.Count == 0 ? 1 : evaluation.Histories.Max(item => item.Version) + 1;
        var overridesByStudent = memberScoreOverrides.ToDictionary(item => item.StudentId);
        var history = new EvaluationHistory
        {
            Version = nextVersion,
            Action = action,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                Status = evaluation.Status.ToString().ToUpperInvariant(),
                evaluation.TotalScore,
                evaluation.OverallFeedback,
                RubricScores = rubricScores.Select(item => new
                {
                    item.CriterionKey,
                    CriterionName = rubric.Criteria
                        .FirstOrDefault(criterion => criterion.Key == item.CriterionKey)?.Name ?? item.CriterionKey,
                    item.Score,
                    item.Comment,
                }).ToArray(),
                MemberScores = ActiveMembers(team).Select(member => new
                {
                    member.StudentId,
                    member.ClassStudent.Student.FullName,
                    RollNumber = member.ClassStudent.Student.RollNumber ?? string.Empty,
                    Score = overridesByStudent.TryGetValue(member.StudentId, out var scoreOverride)
                        ? scoreOverride.Score
                        : evaluation.TotalScore,
                    IsOverridden = overridesByStudent.ContainsKey(member.StudentId),
                }).ToArray(),
            }),
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
            .Include(item => item.Project).ThenInclude(item => item.Team).ThenInclude(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.Project).ThenInclude(item => item.Team).ThenInclude(item => item.TeamMembers)
                .ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.Project).ThenInclude(item => item.Team).ThenInclude(item => item.MentorAssignments)
                .ThenInclude(item => item.MentorProfile)
            .Include(item => item.Rubric).ThenInclude(item => item.Checkpoint)
            .Include(item => item.Rubric).ThenInclude(item => item.Criteria)
            .Include(item => item.Evaluator)
            .Include(item => item.Details).ThenInclude(item => item.RubricCriterion)
            .Include(item => item.MemberScores)
            .Include(item => item.Histories).ThenInclude(item => item.ChangedBy);
    }

    private static CourseAssessmentEvaluationResponse ToCourseAssessmentResponse(
        Rubric assessment, Evaluation? evaluation, Team team, Guid userId, string role)
    {
        var canViewScore = evaluation is not null &&
                           EvaluationVisibilityRules.CanViewTeamScore(role, evaluation.Status);
        return new CourseAssessmentEvaluationResponse
        {
            AssessmentId = assessment.Id,
            Name = assessment.Name,
            Weight = assessment.CourseWeight,
            EvaluationId = evaluation?.Id,
            EvaluatorId = evaluation?.EvaluatorId,
            Score = canViewScore ? evaluation!.TotalScore : null,
            MemberScores = evaluation is null ? null : VisibleMemberScores(evaluation, team, userId, role),
            Status = evaluation?.Status.ToString().ToUpperInvariant() ?? "NOT_GRADED",
            UpdatedAt = evaluation is null ? null : evaluation.UpdatedAt ?? evaluation.CreatedAt
        };
    }

    private static WorkspaceCheckpointEvaluationConfigResponse ToCheckpointResponse(
        Checkpoint checkpoint, Rubric rubric, Team? team, Guid userId, string role) => new()
    {
        Number = checkpoint.CheckpointNumber,
        Title = checkpoint.Name,
        ShortDescription = checkpoint.Description,
        CourseWeight = checkpoint.CourseWeight,
        Rubrics = rubric.Criteria.OrderBy(item => item.DisplayOrder).Select(item => new WorkspaceCheckpointEvaluationCriterionResponse
        {
            Key = item.Key,
            Label = item.Name,
            Description = item.Description,
            Weight = item.Weight,
            MaxScore = item.MaxScore,
            Levels = DeserializeLevels(item.LevelsJson)
        }).ToArray(),
        Members = team is null
            ? Array.Empty<WorkspaceCheckpointEvaluationMemberResponse>()
            : ActiveMembers(team).Where(item => EvaluationVisibilityRules.IsInternalViewer(role) ||
                (IsRole(role, SystemRoles.Student) && item.ClassStudent.Student.UserId == userId))
                .Select(item => new WorkspaceCheckpointEvaluationMemberResponse
            {
                StudentId = item.StudentId,
                FullName = item.ClassStudent.Student.FullName,
                RollNumber = item.ClassStudent.Student.RollNumber ?? string.Empty
            }).ToArray()
    };

    private static WorkspaceCheckpointEvaluationResponse ToEvaluationResponse(
        Evaluation evaluation, Team? team, Guid userId, string role)
    {
        var canViewTeamScore = EvaluationVisibilityRules.CanViewTeamScore(role, evaluation.Status);
        var canViewCriterionScores = EvaluationVisibilityRules.CanViewCriterionScores(role, evaluation.Status);
        var memberScores = VisibleMemberScores(evaluation, team, userId, role);

        return new WorkspaceCheckpointEvaluationResponse
        {
            Id = evaluation.Id,
            LecturerId = ToUserResponse(evaluation.Evaluator),
            EvaluatorRole = evaluation.EvaluatorRole.ToString(),
            Status = evaluation.Status.ToString().ToUpperInvariant(),
            CheckpointTotal = canViewTeamScore ? evaluation.TotalScore : null,
            OverallFeedback = evaluation.OverallFeedback,
            UpdatedAt = evaluation.UpdatedAt ?? evaluation.CreatedAt,
            RubricScores = evaluation.Details.OrderBy(item => item.RubricCriterion.DisplayOrder)
                .Select(item => new WorkspaceCheckpointCriterionScoreResponse
                {
                    CriterionKey = item.RubricCriterion.Key,
                    CriterionName = item.RubricCriterion.Name,
                    Score = canViewCriterionScores ? item.Score : null,
                    Comment = item.Comment
                }).ToArray(),
            MemberScores = memberScores,
        };
    }

    private static IReadOnlyCollection<WorkspaceCheckpointEvaluationMemberScoreResponse>? VisibleMemberScores(
        Evaluation evaluation, Team? team, Guid userId, string role)
    {
        if (team is null) return null;

        IEnumerable<TeamMember> members;
        if (EvaluationVisibilityRules.CanViewAllMemberScores(role))
        {
            members = ActiveMembers(team);
        }
        else if (EvaluationVisibilityRules.CanViewOwnMemberScore(role, evaluation.Status))
        {
            members = ActiveMembers(team)
                .Where(member => member.ClassStudent.Student.UserId == userId);
        }
        else
        {
            return null;
        }

        return members.Select(member =>
        {
            var scoreOverride = evaluation.MemberScores.FirstOrDefault(item => item.StudentId == member.StudentId);
            return new WorkspaceCheckpointEvaluationMemberScoreResponse
            {
                StudentId = member.StudentId,
                Score = scoreOverride?.Score ?? evaluation.TotalScore,
                IsOverridden = scoreOverride is not null,
            };
        }).ToArray();
    }

    private static IEnumerable<TeamMember> ActiveMembers(Team team) => team.TeamMembers
        .Where(item => item.CountsTowardActiveTeam && item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active)
        .OrderBy(item => item.ClassStudent.Student.FullName)
        .ThenBy(item => item.StudentId);

    private static IReadOnlyCollection<WorkspaceCheckpointEvaluationHistoryResponse> ToHistoryResponses(
        IReadOnlyCollection<EvaluationHistory> histories, Team team, Guid userId, string role)
    {
        var previousByEvaluation = new Dictionary<Guid, HistorySnapshot>();
        var responses = new List<WorkspaceCheckpointEvaluationHistoryResponse>(histories.Count);
        foreach (var history in histories.OrderBy(item => item.ChangedAt)
                     .ThenBy(item => item.EvaluationId).ThenBy(item => item.Version))
        {
            var snapshot = ParseHistorySnapshot(history.SnapshotJson);
            previousByEvaluation.TryGetValue(history.EvaluationId, out var previous);
            var changes = BuildHistoryChanges(previous, snapshot, team, userId, role);
            responses.Add(new WorkspaceCheckpointEvaluationHistoryResponse
            {
                Id = history.Id,
                Action = history.Action.ToString().ToUpperInvariant(),
                Version = history.Version,
                ChangedBy = ToUserResponse(history.ChangedBy),
                CreatedAt = history.ChangedAt,
                Note = history.Note,
                Changes = changes,
            });
            previousByEvaluation[history.EvaluationId] = snapshot;
        }

        // Build each diff chronologically, then present the newest audit entry first.
        responses.Reverse();
        return responses;
    }

    private static IReadOnlyCollection<WorkspaceCheckpointEvaluationHistoryChangeResponse> BuildHistoryChanges(
        HistorySnapshot? previous, HistorySnapshot current, Team team, Guid userId, string role)
    {
        var changes = new List<WorkspaceCheckpointEvaluationHistoryChangeResponse>();
        AddChange(changes, "STATUS", "status", "Status", previous?.Status, current.Status);
        AddChange(changes, "SCORE", "totalScore", "Team score",
            FormatScore(previous?.TotalScore), FormatScore(current.TotalScore));
        AddChange(changes, "FEEDBACK", "overallFeedback", "Overall feedback",
            previous?.OverallFeedback, current.OverallFeedback);

        if (current.HasRubricScores || previous?.HasRubricScores == true)
        {
            var previousCriteria = previous?.RubricScores.ToDictionary(item => item.CriterionKey, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, HistoryCriterionSnapshot>(StringComparer.OrdinalIgnoreCase);
            var currentCriteria = current.RubricScores.ToDictionary(item => item.CriterionKey, StringComparer.OrdinalIgnoreCase);
            foreach (var key in previousCriteria.Keys.Union(currentCriteria.Keys, StringComparer.OrdinalIgnoreCase))
            {
                previousCriteria.TryGetValue(key, out var oldCriterion);
                currentCriteria.TryGetValue(key, out var newCriterion);
                var label = newCriterion?.CriterionName ?? oldCriterion?.CriterionName ?? key;
                AddChange(changes, "SCORE", $"rubricScore:{key}", $"{label} score",
                    FormatScore(oldCriterion?.Score), FormatScore(newCriterion?.Score));
                AddChange(changes, "FEEDBACK", $"rubricComment:{key}", $"{label} comment",
                    oldCriterion?.Comment, newCriterion?.Comment);
            }
        }

        if (current.HasMemberScores || previous?.HasMemberScores == true)
        {
            var previousMembers = previous?.MemberScores.ToDictionary(item => item.StudentId)
                ?? new Dictionary<Guid, HistoryMemberSnapshot>();
            var currentMembers = current.MemberScores.ToDictionary(item => item.StudentId);
            var activeMembers = ActiveMembers(team).ToDictionary(item => item.StudentId);
            foreach (var studentId in previousMembers.Keys.Union(currentMembers.Keys))
            {
                previousMembers.TryGetValue(studentId, out var oldMember);
                currentMembers.TryGetValue(studentId, out var newMember);
                activeMembers.TryGetValue(studentId, out var activeMember);
                var fullName = newMember?.FullName ?? oldMember?.FullName
                    ?? activeMember?.ClassStudent.Student.FullName ?? studentId.ToString();
                var rollNumber = newMember?.RollNumber ?? oldMember?.RollNumber
                    ?? activeMember?.ClassStudent.Student.RollNumber;
                var label = string.IsNullOrWhiteSpace(rollNumber)
                    ? $"{fullName} score"
                    : $"{fullName} ({rollNumber}) score";
                AddChange(changes, "SCORE", $"memberScore:{studentId}", label,
                    FormatScore(oldMember?.Score), FormatScore(newMember?.Score));
            }
        }

        if (EvaluationVisibilityRules.IsInternalViewer(role)) return changes;
        if (!string.Equals(current.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<WorkspaceCheckpointEvaluationHistoryChangeResponse>();

        var currentStudentId = ActiveMembers(team)
            .Where(member => member.ClassStudent.Student.UserId == userId)
            .Select(member => (Guid?)member.StudentId)
            .FirstOrDefault();
        return changes.Where(change =>
            change.Category == "STATUS" ||
            change.Category == "FEEDBACK" ||
            change.Field == "totalScore" ||
            (change.Field.StartsWith("rubricScore:", StringComparison.Ordinal) &&
             EvaluationVisibilityRules.CanViewCriterionScores(role, EvaluationStatus.Published)) ||
            (currentStudentId.HasValue && change.Field == $"memberScore:{currentStudentId.Value}"))
            .ToArray();
    }

    private static void AddChange(
        ICollection<WorkspaceCheckpointEvaluationHistoryChangeResponse> changes,
        string category, string field, string label, string? previousValue, string? currentValue)
    {
        var oldValue = NormalizeHistoryValue(previousValue);
        var newValue = NormalizeHistoryValue(currentValue);
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) return;
        changes.Add(new WorkspaceCheckpointEvaluationHistoryChangeResponse
        {
            Category = category,
            Field = field,
            Label = label,
            PreviousValue = oldValue,
            CurrentValue = newValue,
        });
    }

    private static string? NormalizeHistoryValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FormatScore(decimal? value) =>
        value?.ToString("0.##", CultureInfo.InvariantCulture);

    private static HistorySnapshot ParseHistorySnapshot(string snapshotJson)
    {
        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
            var root = document.RootElement;
            var status = ReadStatus(root);
            var totalScore = ReadDecimal(root, "TotalScore");
            var overallFeedback = ReadString(root, "OverallFeedback");
            var hasRubricScores = root.TryGetProperty("RubricScores", out var rubricElement) &&
                                  rubricElement.ValueKind == JsonValueKind.Array;
            var rubricScores = hasRubricScores
                ? rubricElement.EnumerateArray().Select(item => new HistoryCriterionSnapshot(
                    ReadString(item, "CriterionKey") ?? string.Empty,
                    ReadString(item, "CriterionName") ?? ReadString(item, "CriterionKey") ?? "Criterion",
                    ReadDecimal(item, "Score"),
                    ReadString(item, "Comment"))).Where(item => !string.IsNullOrWhiteSpace(item.CriterionKey)).ToArray()
                : Array.Empty<HistoryCriterionSnapshot>();

            var memberProperty = root.TryGetProperty("MemberScores", out var memberElement)
                ? "MemberScores"
                : root.TryGetProperty("MemberScoreOverrides", out memberElement)
                    ? "MemberScoreOverrides"
                    : null;
            var hasMemberScores = memberProperty is not null && memberElement.ValueKind == JsonValueKind.Array;
            var memberScores = hasMemberScores
                ? memberElement.EnumerateArray().Select(item => new HistoryMemberSnapshot(
                    ReadGuid(item, "StudentId"),
                    ReadString(item, "FullName"),
                    ReadString(item, "RollNumber"),
                    ReadDecimal(item, "Score"),
                    ReadBoolean(item, "IsOverridden") ?? memberProperty == "MemberScoreOverrides"))
                    .Where(item => item.StudentId != Guid.Empty).ToArray()
                : Array.Empty<HistoryMemberSnapshot>();

            return new HistorySnapshot(status, totalScore, overallFeedback, rubricScores,
                memberScores, hasRubricScores, hasMemberScores);
        }
        catch (JsonException)
        {
            return HistorySnapshot.Empty;
        }
    }

    private static string? ReadStatus(JsonElement element)
    {
        if (!element.TryGetProperty("Status", out var value)) return null;
        if (value.ValueKind == JsonValueKind.String) return value.GetString()?.ToUpperInvariant();
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric) &&
            Enum.IsDefined(typeof(EvaluationStatus), numeric))
            return ((EvaluationStatus)numeric).ToString().ToUpperInvariant();
        return value.ToString().ToUpperInvariant();
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static decimal? ReadDecimal(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out var number) ? number : null;

    private static Guid ReadGuid(JsonElement element, string propertyName) =>
        Guid.TryParse(ReadString(element, propertyName), out var value) ? value : Guid.Empty;

    private static bool? ReadBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

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
    private sealed record ValidatedMemberScore(Guid StudentId, decimal Score);
    private sealed record ValidatedSave(EvaluationStatus Status, string? OverallFeedback, decimal TotalScore,
        IReadOnlyCollection<ValidatedScore> Scores, IReadOnlyCollection<ValidatedMemberScore> MemberScoreOverrides);
    private sealed record HistoryCriterionSnapshot(
        string CriterionKey, string CriterionName, decimal? Score, string? Comment);
    private sealed record HistoryMemberSnapshot(
        Guid StudentId, string? FullName, string? RollNumber, decimal? Score, bool IsOverridden);
    private sealed record HistorySnapshot(
        string? Status,
        decimal? TotalScore,
        string? OverallFeedback,
        IReadOnlyCollection<HistoryCriterionSnapshot> RubricScores,
        IReadOnlyCollection<HistoryMemberSnapshot> MemberScores,
        bool HasRubricScores,
        bool HasMemberScores)
    {
        public static HistorySnapshot Empty { get; } = new(
            null, null, null,
            Array.Empty<HistoryCriterionSnapshot>(),
            Array.Empty<HistoryMemberSnapshot>(),
            false, false);
    }
}
