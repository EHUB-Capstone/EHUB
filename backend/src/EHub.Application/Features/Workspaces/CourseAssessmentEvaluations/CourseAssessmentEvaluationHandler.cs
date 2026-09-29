using EHub.Application.Common.Interfaces.Persistence;
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
                return ToResponse(assessment, evaluation);
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

        var assessment = await context.Rubrics.FirstOrDefaultAsync(item =>
            item.Id == assessmentId && item.CourseId == team.Class.CourseId && item.ClassId == null &&
            item.CheckpointId == null && item.Status == RubricStatus.Active, cancellationToken);
        if (assessment is null)
            return Result.Failure<CourseAssessmentEvaluationResponse>(
                ErrorCodes.CommonNotFoundError, "The course assessment was not found.");

        var evaluation = await context.Evaluations
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
                PublishedAt = now,
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
            evaluation.PublishedAt = now;
            evaluation.UpdatedAt = now;
            evaluation.UpdatedBy = userId;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success(ToResponse(assessment, evaluation));
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

    private static CourseAssessmentEvaluationResponse ToResponse(Rubric assessment, Evaluation? evaluation) => new()
    {
        AssessmentId = assessment.Id,
        Name = assessment.Name,
        Weight = assessment.CourseWeight,
        EvaluationId = evaluation?.Id,
        Score = evaluation?.TotalScore,
        Status = evaluation?.Status.ToString().ToUpperInvariant() ?? "NOT_GRADED",
        UpdatedAt = evaluation?.UpdatedAt ?? evaluation?.CreatedAt,
    };

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
