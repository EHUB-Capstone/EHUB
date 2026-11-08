using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Workspaces;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Teams.FeedbackOverview;

/// <summary>
/// Aggregates checkpoint feedback (comment threads and written evaluation feedback) of one team.
/// Scores are never returned. Mentors only receive threads they started themselves.
/// </summary>
public sealed class TeamFeedbackOverviewHandler(IApplicationDbContext context) : ITeamFeedbackOverviewHandler
{
    private const string TechnicalReviewPrefix = "TECHNICAL REVIEW\n";

    public async Task<Result<TeamFeedbackOverviewDto>> GetAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var accessibleTeams = AccessibleTeams(userId, role);
        if (accessibleTeams is null)
        {
            return AccessDenied();
        }

        var team = await accessibleTeams
            .Where(item => item.Id == teamId)
            .Select(item => new { item.Id, item.TeamName, item.Class.CourseId })
            .SingleOrDefaultAsync(cancellationToken);

        if (team is null)
        {
            if (IsRole(role, SystemRoles.Admin))
            {
                return Result.Failure<TeamFeedbackOverviewDto>(
                    new Error(ErrorCodes.TeamNotFound, "The requested team was not found."));
            }

            return AccessDenied();
        }

        if (IsRole(role, SystemRoles.Student) &&
            !await ProjectWorkspaceApprovalRules.IsApprovedAsync(context, teamId, cancellationToken))
        {
            return AccessDenied();
        }

        var checkpoints = await context.Checkpoints
            .AsNoTracking()
            .Where(checkpoint =>
                checkpoint.CourseId == team.CourseId &&
                checkpoint.ClassId == null &&
                checkpoint.Status != CheckpointStatus.Archived)
            .OrderBy(checkpoint => checkpoint.CheckpointNumber)
            .ToListAsync(cancellationToken);

        var threadsByCheckpoint = await LoadThreadsAsync(teamId, userId, role, cancellationToken);
        var evaluationsByCheckpoint = await LoadEvaluationsAsync(teamId, userId, role, cancellationToken);

        return Result.Success(new TeamFeedbackOverviewDto
        {
            TeamId = team.Id,
            TeamName = team.TeamName,
            Checkpoints = checkpoints
                .Select(checkpoint => new TeamFeedbackCheckpointDto
                {
                    CheckpointId = checkpoint.Id,
                    CheckpointNumber = checkpoint.CheckpointNumber,
                    Title = checkpoint.Name,
                    Threads = threadsByCheckpoint.GetValueOrDefault(checkpoint.Id) ?? [],
                    Evaluations = evaluationsByCheckpoint.GetValueOrDefault(checkpoint.Id) ?? []
                })
                .ToArray()
        });
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<TeamFeedbackThreadDto>>> LoadThreadsAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken)
    {
        var feedbacks = await context.SubmissionFeedbacks
            .AsNoTracking()
            .Include(feedback => feedback.Submission)
            .Include(feedback => feedback.Creator)
            .ThenInclude(user => user!.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Where(feedback => feedback.Submission.TeamId == teamId)
            .OrderBy(feedback => feedback.CreatedAt)
            .ToListAsync(cancellationToken);

        var byId = feedbacks.ToDictionary(feedback => feedback.Id);
        var repliesByRoot = new Dictionary<Guid, List<SubmissionFeedback>>();
        var roots = new List<SubmissionFeedback>();

        foreach (var feedback in feedbacks)
        {
            var root = ResolveRoot(feedback, byId);
            if (root.Id == feedback.Id)
            {
                roots.Add(feedback);
                continue;
            }

            if (!repliesByRoot.TryGetValue(root.Id, out var replies))
            {
                replies = [];
                repliesByRoot[root.Id] = replies;
            }

            replies.Add(feedback);
        }

        // Mentors only see the threads they started themselves, together with the replies.
        var isMentor = IsRole(role, SystemRoles.Mentor);
        var visibleRoots = isMentor
            ? roots.Where(root => root.CreatedById == userId)
            : roots;

        return visibleRoots
            .GroupBy(root => root.Submission.CheckpointId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<TeamFeedbackThreadDto>)group
                    .OrderBy(root => root.CreatedAt)
                    .Select(root => new TeamFeedbackThreadDto
                    {
                        Id = root.Id,
                        Content = root.Content,
                        IsTechnicalReview = root.Content.StartsWith(TechnicalReviewPrefix, StringComparison.Ordinal),
                        Resolved = root.Resolved,
                        CreatedAt = root.CreatedAt,
                        SubmissionVersion = root.Submission.VersionNumber,
                        Author = ToAuthor(root.Creator),
                        Replies = (repliesByRoot.GetValueOrDefault(root.Id) ?? [])
                            .Select(reply => new TeamFeedbackReplyDto
                            {
                                Id = reply.Id,
                                Content = reply.Content,
                                CreatedAt = reply.CreatedAt,
                                Author = ToAuthor(reply.Creator)
                            })
                            .ToArray()
                    })
                    .ToArray());
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<TeamFeedbackEvaluationDto>>> LoadEvaluationsAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken)
    {
        // Mentors do not author evaluations and must only see their own feedback.
        if (IsRole(role, SystemRoles.Mentor))
        {
            return [];
        }

        var isLecturer = IsRole(role, SystemRoles.Lecturer);
        var evaluations = await context.Evaluations
            .AsNoTracking()
            .Include(evaluation => evaluation.Rubric)
            .Include(evaluation => evaluation.Evaluator)
            .ThenInclude(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Include(evaluation => evaluation.Details)
            .ThenInclude(detail => detail.RubricCriterion)
            .Where(evaluation =>
                evaluation.Project.TeamId == teamId &&
                evaluation.Rubric.CheckpointId != null &&
                (evaluation.Status != EvaluationStatus.Draft ||
                 (isLecturer && evaluation.EvaluatorId == userId)))
            .ToListAsync(cancellationToken);

        // One current evaluation per evaluator and rubric (the latest submission version wins).
        return evaluations
            .GroupBy(evaluation => new { evaluation.RubricId, evaluation.EvaluatorId })
            .Select(group => group.OrderByDescending(item => item.CreatedAt).First())
            .GroupBy(evaluation => evaluation.Rubric.CheckpointId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<TeamFeedbackEvaluationDto>)group
                    .OrderBy(evaluation => evaluation.SubmittedAt ?? evaluation.CreatedAt)
                    .Select(evaluation => new TeamFeedbackEvaluationDto
                    {
                        Id = evaluation.Id,
                        Status = evaluation.Status.ToString(),
                        Evaluator = ToAuthor(evaluation.Evaluator),
                        OverallFeedback = evaluation.OverallFeedback,
                        Strengths = evaluation.Strengths,
                        Weaknesses = evaluation.Weaknesses,
                        Suggestions = evaluation.Suggestions,
                        SubmittedAt = evaluation.SubmittedAt,
                        PublishedAt = evaluation.PublishedAt,
                        CriterionComments = evaluation.Details
                            .Where(detail => !string.IsNullOrWhiteSpace(detail.Comment))
                            .OrderBy(detail => detail.RubricCriterion.DisplayOrder)
                            .Select(detail => new TeamFeedbackCriterionCommentDto
                            {
                                CriterionName = detail.RubricCriterion.Name,
                                Comment = detail.Comment!
                            })
                            .ToArray()
                    })
                    .ToArray());
    }

    private IQueryable<Team>? AccessibleTeams(Guid userId, string role)
    {
        var query = context.Teams.AsNoTracking();
        if (IsRole(role, SystemRoles.Admin))
        {
            return query;
        }

        if (IsRole(role, SystemRoles.Lecturer))
        {
            return query.Where(team => team.Class.PrimaryLecturerId == userId ||
                team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId));
        }

        if (IsRole(role, SystemRoles.Mentor))
        {
            return query.Where(team => team.MentorAssignments.Any(assignment =>
                assignment.MentorProfile.UserId == userId &&
                assignment.Status == MentorAssignmentStatus.Active &&
                assignment.EndedAt == null));
        }

        if (IsRole(role, SystemRoles.Student))
        {
            return query.Where(team => team.TeamMembers.Any(member =>
                member.CountsTowardActiveTeam &&
                member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active &&
                member.ClassStudent.Student.UserId == userId));
        }

        return null;
    }

    private static SubmissionFeedback ResolveRoot(
        SubmissionFeedback feedback, IReadOnlyDictionary<Guid, SubmissionFeedback> byId)
    {
        var current = feedback;
        var visited = new HashSet<Guid> { current.Id };
        while (current.ParentFeedbackId is { } parentId &&
               byId.TryGetValue(parentId, out var parent) &&
               visited.Add(parent.Id))
        {
            current = parent;
        }

        return current;
    }

    private static TeamFeedbackAuthorDto ToAuthor(User? user) => user is null
        ? new TeamFeedbackAuthorDto()
        : new TeamFeedbackAuthorDto { Id = user.Id, Name = user.FullName, Role = ResolveRole(user) };

    private static string ResolveRole(User user)
    {
        var roles = user.UserRoles.Select(userRole => userRole.Role.Name).ToArray();
        foreach (var candidate in new[] { SystemRoles.Admin, SystemRoles.Lecturer, SystemRoles.Mentor, SystemRoles.Student })
        {
            if (roles.Any(name => IsRole(name, candidate)))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);

    private static Result<TeamFeedbackOverviewDto> AccessDenied() =>
        Result.Failure<TeamFeedbackOverviewDto>(new Error(
            ErrorCodes.ClassAccessDenied,
            "You do not have access to the feedback of this team."));
}
