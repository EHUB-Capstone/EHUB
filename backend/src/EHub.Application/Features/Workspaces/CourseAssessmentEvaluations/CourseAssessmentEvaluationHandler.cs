using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Workspaces.CheckpointEvaluations;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CourseAssessmentEvaluations;

public sealed class CourseAssessmentEvaluationHandler(IApplicationDbContext context)
    : ICourseAssessmentEvaluationHandler
{
    public async Task<Result<CourseAssessmentEvaluationListResponse>> GetAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var access = await LoadTeamAsync(teamId, userId, role, cancellationToken);
        if (access.IsFailure)
            return Result.Failure<CourseAssessmentEvaluationListResponse>(access.Error);

        var team = access.Value;
        var assessments = await context.Rubrics
            .AsNoTracking()
            .Where(item => item.CourseId == team.Class.CourseId && item.ClassId == null &&
                           item.CheckpointId == null && item.Status == RubricStatus.Active)
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        var assessmentIds = assessments.Select(item => item.Id).ToArray();
        var evaluations = team.Project is null || assessmentIds.Length == 0
            ? Array.Empty<Evaluation>()
            : await context.Evaluations
                .AsNoTracking()
                .Include(item => item.MemberScores)
                .Where(item => item.ProjectId == team.Project.Id && assessmentIds.Contains(item.RubricId))
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .ToArrayAsync(cancellationToken);
        var canGrade = CanGrade(role, team, userId);

        return Result.Success(new CourseAssessmentEvaluationListResponse
        {
            Assessments = assessments.Select(assessment =>
            {
                var evaluation = evaluations
                    .Where(item => item.RubricId == assessment.Id &&
                                   (canGrade || item.Status is EvaluationStatus.Submitted or EvaluationStatus.Published))
                    .OrderByDescending(item => item.Status is EvaluationStatus.Submitted or EvaluationStatus.Published)
                    .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                    .FirstOrDefault();
                return ToResponse(assessment, evaluation, team, userId, role);
            }).ToArray(),
        });
    }

    public async Task<Result<CourseAssessmentEvaluationResponse>> SaveAsync(
        Guid teamId, Guid assessmentId, SaveCourseAssessmentEvaluationRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (request.Score is < 0 or > 10)
            return Result.Failure<CourseAssessmentEvaluationResponse>(
                ErrorCodes.WorkspaceValidationError, "The assessment score must be between 0 and 10.");

        var access = await LoadTeamAsync(teamId, userId, role, cancellationToken);
        if (access.IsFailure)
            return Result.Failure<CourseAssessmentEvaluationResponse>(access.Error);
        var team = access.Value;
        if (!CanGrade(role, team, userId))
            return Denied<CourseAssessmentEvaluationResponse>("Only an assigned lecturer can grade this assessment.");
        if (team.Project is null)
            return Result.Failure<CourseAssessmentEvaluationResponse>(
                ErrorCodes.WorkspaceNotFound, "The team project was not found.");

        var activeStudentIds = ActiveMembers(team).Select(item => item.StudentId).ToHashSet();
        if (request.MemberScores.GroupBy(item => item.StudentId).Any(group => group.Count() > 1))
            return Result.Failure<CourseAssessmentEvaluationResponse>(
                ErrorCodes.WorkspaceValidationError, "A team member can only have one individual assessment score.");
        if (request.MemberScores.Any(item => item.Score is < 0 or > 10))
            return Result.Failure<CourseAssessmentEvaluationResponse>(
                ErrorCodes.WorkspaceValidationError, "Each individual member score must be between 0 and 10.");
        if (request.MemberScores.Any(item => !activeStudentIds.Contains(item.StudentId)))
            return Result.Failure<CourseAssessmentEvaluationResponse>(
                ErrorCodes.WorkspaceValidationError, "One or more individual scores belong to a student outside this team.");

        var assessment = await context.Rubrics.FirstOrDefaultAsync(item =>
            item.Id == assessmentId && item.CourseId == team.Class.CourseId && item.ClassId == null &&
            item.CheckpointId == null && item.Status == RubricStatus.Active, cancellationToken);
        if (assessment is null)
            return Result.Failure<CourseAssessmentEvaluationResponse>(
                ErrorCodes.CommonNotFoundError, "The course assessment was not found.");

        var evaluation = await context.Evaluations
            .Include(item => item.MemberScores)
            .Where(item => item.ProjectId == team.Project.Id && item.RubricId == assessment.Id &&
                           item.EvaluatorId == userId)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;
        if (evaluation is null)
        {
            evaluation = new Evaluation
            {
                ProjectId = team.Project.Id,
                RubricId = assessment.Id,
                EvaluatorId = userId,
                EvaluatorRole = EvaluatorRole.Lecturer,
                TotalScore = Math.Round(request.Score, 2),
                MaxTotalScore = 10,
                Status = EvaluationStatus.Submitted,
                SubmittedAt = now,
                PublishedAt = null,
                CreatedAt = now,
                CreatedBy = userId,
            };
            context.Evaluations.Add(evaluation);
        }
        else
        {
            evaluation.TotalScore = Math.Round(request.Score, 2);
            evaluation.MaxTotalScore = 10;
            evaluation.Status = EvaluationStatus.Submitted;
            evaluation.SubmittedAt ??= now;
            evaluation.PublishedAt = null;
            evaluation.UpdatedAt = now;
            evaluation.UpdatedBy = userId;
        }

        AddOrUpdateMemberScores(evaluation, request.MemberScores, request.Score, userId, now);

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(assessment, evaluation, team, userId, role));
    }

    private async Task<Result<Team>> LoadTeamAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken)
    {
        var team = await context.Teams
            .Include(item => item.Project)
            .Include(item => item.Class).ThenInclude(item => item.ClassLecturers)
            .Include(item => item.TeamMembers).ThenInclude(item => item.ClassStudent).ThenInclude(item => item.Student)
            .Include(item => item.MentorAssignments).ThenInclude(item => item.MentorProfile)
            .FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        return team is not null && CanView(role, team, userId)
            ? Result.Success(team)
            : Denied<Team>("You do not have access to this team's course assessments.");
    }

    private void AddOrUpdateMemberScores(
        Evaluation evaluation,
        IReadOnlyCollection<WorkspaceCheckpointMemberScoreInput> memberScores,
        decimal teamScore,
        Guid userId,
        DateTime now)
    {
        var overrides = memberScores
            .Where(item => Math.Round(item.Score, 2) != Math.Round(teamScore, 2))
            .ToDictionary(item => item.StudentId, item => Math.Round(item.Score, 2));

        foreach (var removed in evaluation.MemberScores
                     .Where(item => !overrides.ContainsKey(item.StudentId))
                     .ToArray())
        {
            evaluation.MemberScores.Remove(removed);
            context.EvaluationMemberScores.Remove(removed);
        }

        var existingByStudent = evaluation.MemberScores.ToDictionary(item => item.StudentId);
        foreach (var (studentId, score) in overrides)
        {
            if (existingByStudent.TryGetValue(studentId, out var existing))
            {
                existing.Score = score;
                existing.UpdatedAt = now;
                existing.UpdatedBy = userId;
                continue;
            }

            var memberScore = new EvaluationMemberScore
            {
                StudentId = studentId,
                Score = score,
                CreatedAt = now,
                CreatedBy = userId,
            };
            evaluation.MemberScores.Add(memberScore);
            context.EvaluationMemberScores.Add(memberScore);
        }
    }

    private static CourseAssessmentEvaluationResponse ToResponse(
        Rubric assessment, Evaluation? evaluation, Team team, Guid userId, string role) => new()
    {
        AssessmentId = assessment.Id,
        Name = assessment.Name,
        Weight = assessment.CourseWeight,
        EvaluationId = evaluation?.Id,
        EvaluatorId = evaluation?.EvaluatorId,
        Score = evaluation is not null && EvaluationVisibilityRules.CanViewTeamScore(role, evaluation.Status)
            ? evaluation.TotalScore
            : null,
        MemberScores = VisibleMemberScores(evaluation, team, userId, role),
        Status = evaluation?.Status.ToString().ToUpperInvariant() ?? "NOT_GRADED",
        UpdatedAt = evaluation?.UpdatedAt ?? evaluation?.CreatedAt,
    };

    private static IReadOnlyCollection<WorkspaceCheckpointEvaluationMemberScoreResponse>? VisibleMemberScores(
        Evaluation? evaluation, Team team, Guid userId, string role)
    {
        if (evaluation is null) return null;

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

    private static bool CanView(string role, Team team, Guid userId) =>
        IsRole(role, SystemRoles.Admin) ||
        (IsRole(role, SystemRoles.Lecturer) &&
         (team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(item => item.LecturerId == userId))) ||
        (IsRole(role, SystemRoles.Mentor) && team.MentorAssignments.Any(item =>
            item.Status == MentorAssignmentStatus.Active && item.EndedAt == null && item.MentorProfile.UserId == userId)) ||
        (IsRole(role, SystemRoles.Student) && team.TeamMembers.Any(item =>
            item.CountsTowardActiveTeam && item.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active &&
            item.ClassStudent.Student.UserId == userId));

    private static bool CanGrade(string role, Team team, Guid userId) =>
        IsRole(role, SystemRoles.Lecturer) &&
        (team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(item => item.LecturerId == userId));

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);

    private static Result<T> Denied<T>(string message) =>
        Result.Failure<T>(ErrorCodes.WorkspaceAccessDenied, message);
}
