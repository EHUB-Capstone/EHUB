using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.Common;
using EHub.Application.Features.ProjectData.GetProjectDataTrends;
using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectEvidence;

/// <summary>
/// The academic evidence of one project for staff: its official course score with the published feedback of every
/// graded part, and the mentors of its team with their expertise and finished sessions. Read-only.
/// </summary>
public sealed class GetProjectEvidenceQueryHandler(IApplicationDbContext context) : IGetProjectEvidenceQueryHandler
{
    /// <summary>Most expertise tags shown for one mentor; keeps the payload and the dialog bounded.</summary>
    internal const int MaxExpertisePerMentor = 12;

    public async Task<Result<ProjectEvidenceResponse>> HandleAsync(
        Guid projectId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Failure(ErrorCodes.ProjectDataAccessDenied, "Only administrators and lecturers can view project data.");
        }

        // Same rule as the other project data views: the project must be in the feature scope, and a lecturer must teach its class.
        var project = await ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin: true)
            .Where(item => item.Id == projectId)
            .Select(item => new { item.TeamId, CourseId = item.Team.Class.CourseId })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null)
        {
            return Failure(ErrorCodes.ProjectDataNotFound, "The project was not found.");
        }

        if (!isAdmin && !await ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin: false)
                .AnyAsync(item => item.Id == projectId, cancellationToken))
        {
            return Failure(ErrorCodes.ProjectDataAccessDenied, "You can only view projects of classes assigned to you.");
        }

        return Result.Success(new ProjectEvidenceResponse
        {
            ProjectId = projectId,
            Evaluation = await LoadEvaluationAsync(projectId, project.CourseId, cancellationToken),
            Mentoring = await LoadMentoringAsync(project.TeamId, cancellationToken),
        });
    }

    private async Task<ProjectEvidenceEvaluationResponse> LoadEvaluationAsync(
        Guid projectId, Guid courseId, CancellationToken cancellationToken)
    {
        var components = (await ProjectDataScoreLoader.LoadComponentsAsync(context, [courseId], cancellationToken))
            .GetValueOrDefault(courseId) ?? [];
        var evaluations = (await ProjectDataScoreLoader.LoadEvaluationsAsync(context, [projectId], cancellationToken))
            .GetValueOrDefault(projectId) ?? [];

        // The version that decides each part is the one the official score uses; only a published, gradable one shows a score.
        var decided = components
            .OrderBy(item => item.CheckpointNumber ?? int.MaxValue)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => (Part: item, Latest: OfficialCourseScore.LatestFor(item.Component, evaluations)))
            .ToList();

        // Feedback is long text, so it is read only for the published versions that are shown.
        var publishedIds = decided
            .Where(item => IsPublished(item.Latest))
            .Select(item => item.Latest!.Id)
            .ToArray();
        var details = publishedIds.Length == 0
            ? []
            : await context.Evaluations.AsNoTracking()
                .Where(item => publishedIds.Contains(item.Id))
                .Select(item => new { item.Id, item.OverallFeedback, item.PublishedAt })
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        return new ProjectEvidenceEvaluationResponse
        {
            CourseScore = OfficialCourseScore.Calculate(
                components.Select(item => item.Component).ToArray(), evaluations),
            Components = decided.Select(item =>
            {
                var (part, latest) = item;
                var published = IsPublished(latest);
                var detail = published ? details.GetValueOrDefault(latest!.Id) : null;
                return new ProjectEvidenceComponentResponse
                {
                    Name = part.Name,
                    Kind = part.CheckpointNumber.HasValue ? ProjectEvidenceKinds.Checkpoint : ProjectEvidenceKinds.Assessment,
                    CheckpointNumber = part.CheckpointNumber,
                    WeightPercent = part.Component.Weight,
                    Status = StatusOf(latest),
                    Score = published ? Math.Round(OfficialCourseScore.ToScore(latest!), 2, MidpointRounding.AwayFromZero) : null,
                    Feedback = detail is null ? null : ProjectDataQuery.Normalize(detail.OverallFeedback),
                    PublishedAtUtc = detail?.PublishedAt,
                };
            }).ToList(),
        };
    }

    /// <summary>A published version with a positive maximum is the only kind whose score counts.</summary>
    private static bool IsPublished(TrendEvaluation? evaluation) =>
        evaluation is { Status: EvaluationStatus.Published, MaxTotalScore: > 0 };

    private static string StatusOf(TrendEvaluation? latest) =>
        IsPublished(latest) ? ProjectEvidenceStatuses.Published
        : latest is { Status: EvaluationStatus.Submitted } ? ProjectEvidenceStatuses.PendingPublication
        : ProjectEvidenceStatuses.NotGraded;

    private async Task<ProjectEvidenceMentoringResponse> LoadMentoringAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var teamAssignments = context.MentorAssignments.AsNoTracking().Where(item => item.TeamId == teamId);

        // Every mentor the team had, including the ones replaced or removed along the way; assignments that never
        // started (pending) or were cancelled are not mentoring history.
        var assignments = await teamAssignments
            .Where(item => item.Status == MentorAssignmentStatus.Active || item.Status == MentorAssignmentStatus.Ended)
            .Select(item => new
            {
                item.Id,
                item.MentorProfileId,
                item.Slot,
                item.AssignedAt,
                item.EndedAt,
                FullName = item.MentorProfile.User.FullName,
                item.MentorProfile.Organization,
                item.MentorProfile.Expertise,
                item.MentorProfile.StartupDomains,
                item.MentorProfile.TechnologySkills,
            })
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0) return new ProjectEvidenceMentoringResponse();

        // "Current" means exactly what the project list shows for the slot.
        var shown = await ProjectDataMentorResolver.LoadAsync(teamAssignments, cancellationToken);
        var currentIds = shown.Values.Select(row => row.AssignmentId).ToHashSet();

        var assignmentIds = assignments.Select(item => item.Id).ToArray();
        var sessions = await context.MentoringSessions.AsNoTracking()
            .Where(item => assignmentIds.Contains(item.MentorAssignmentId) && item.Status == MentoringSessionStatus.Completed)
            .GroupBy(item => item.MentorAssignmentId)
            .Select(group => new { AssignmentId = group.Key, Count = group.Count(), Last = group.Max(item => item.StartAt) })
            .ToDictionaryAsync(item => item.AssignmentId, cancellationToken);

        var mentors = assignments
            .OrderByDescending(item => currentIds.Contains(item.Id))
            .ThenBy(item => item.Slot)
            .ThenByDescending(item => item.AssignedAt)
            .ThenBy(item => item.Id)
            .Select(item => new ProjectEvidenceMentorResponse
            {
                AssignmentId = item.Id,
                MentorProfileId = item.MentorProfileId,
                FullName = item.FullName,
                Organization = ProjectDataQuery.Normalize(item.Organization),
                Slot = item.Slot.ToString(),
                AssignedAtUtc = item.AssignedAt,
                EndedAtUtc = item.EndedAt,
                IsCurrent = currentIds.Contains(item.Id),
                Expertise = ProjectDataQuery
                    .DistinctSorted([.. item.Expertise, .. item.StartupDomains, .. item.TechnologySkills])
                    .Take(MaxExpertisePerMentor)
                    .ToArray(),
                CompletedSessions = sessions.GetValueOrDefault(item.Id)?.Count ?? 0,
            })
            .ToList();

        return new ProjectEvidenceMentoringResponse
        {
            Mentors = mentors,
            TotalCompletedSessions = sessions.Values.Sum(item => item.Count),
            LastSessionAtUtc = sessions.Count == 0 ? null : sessions.Values.Max(item => item.Last).UtcDateTime,
        };
    }

    private static Result<ProjectEvidenceResponse> Failure(string code, string message) =>
        Result.Failure<ProjectEvidenceResponse>(code, message);
}
