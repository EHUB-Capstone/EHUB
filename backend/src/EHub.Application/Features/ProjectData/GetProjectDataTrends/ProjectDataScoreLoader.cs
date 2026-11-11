using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectDataTrends;

/// <summary>A weighted part of a course score with the name a person can read; <see cref="CheckpointNumber"/> is null for an assessment.</summary>
internal sealed record ScoreComponent(TrendComponent Component, string Name, int? CheckpointNumber);

/// <summary>
/// Loads the official 0-10 course score of projects (see <see cref="OfficialCourseScore"/>). The trends, the
/// project data export and the project evidence all use it, so a score never differs between them.
/// </summary>
internal static class ProjectDataScoreLoader
{
    /// <summary>The score of each project, or null while the project is not graded yet.</summary>
    internal static async Task<Dictionary<Guid, decimal?>> LoadAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<(Guid ProjectId, Guid CourseId)> projects,
        CancellationToken cancellationToken)
    {
        if (projects.Count == 0) return [];

        var courseIds = projects.Select(project => project.CourseId).Distinct().ToArray();
        var projectIds = projects.Select(project => project.ProjectId).ToArray();

        var componentsByCourse = (await LoadComponentsAsync(context, courseIds, cancellationToken))
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyCollection<TrendComponent>)pair.Value.Select(item => item.Component).ToArray());
        var evaluationsByProject = await LoadEvaluationsAsync(context, projectIds, cancellationToken);

        return projects
            .DistinctBy(project => project.ProjectId)
            .ToDictionary(
                project => project.ProjectId,
                project => OfficialCourseScore.Calculate(
                    componentsByCourse[project.CourseId],
                    evaluationsByProject.GetValueOrDefault(project.ProjectId) ?? []));
    }

    /// <summary>
    /// Course-level parts of the score of each course: checkpoints and assessments together carry the 100% weight.
    /// A course without weighted parts still has an (empty) entry.
    /// </summary>
    internal static async Task<Dictionary<Guid, IReadOnlyList<ScoreComponent>>> LoadComponentsAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> courseIds,
        CancellationToken cancellationToken)
    {
        // Plain arrays, as before the evidence reused this: EF translates them to one parameter list in SQL.
        var courses = courseIds.Distinct().ToArray();
        var checkpointComponents = await context.Checkpoints.AsNoTracking()
            .Where(item => item.CourseId.HasValue && courses.Contains(item.CourseId.Value) &&
                           item.ClassId == null && item.Status != CheckpointStatus.Archived && item.CourseWeight > 0)
            .Select(item => new { CourseId = item.CourseId!.Value, item.Id, item.CourseWeight, item.Name, item.CheckpointNumber })
            .ToListAsync(cancellationToken);
        var assessmentComponents = await context.Rubrics.AsNoTracking()
            .Where(item => item.CourseId.HasValue && courses.Contains(item.CourseId.Value) &&
                           item.ClassId == null && item.CheckpointId == null &&
                           item.Status == RubricStatus.Active && item.CourseWeight > 0)
            .Select(item => new { CourseId = item.CourseId!.Value, item.Id, item.CourseWeight, item.Name })
            .ToListAsync(cancellationToken);

        return courses.ToDictionary(
            courseId => courseId,
            courseId => (IReadOnlyList<ScoreComponent>)
            [
                .. checkpointComponents.Where(item => item.CourseId == courseId)
                    .Select(item => new ScoreComponent(
                        new TrendComponent(item.Id, null, item.CourseWeight), item.Name, item.CheckpointNumber)),
                .. assessmentComponents.Where(item => item.CourseId == courseId)
                    .Select(item => new ScoreComponent(
                        new TrendComponent(null, item.Id, item.CourseWeight), item.Name, null)),
            ]);
    }

    /// <summary>
    /// Submitted and published evaluations of each project against course-level rubrics, whatever their status now,
    /// so a semester keeps its score after a rubric is replaced. Projects without any have no entry.
    /// </summary>
    internal static async Task<Dictionary<Guid, IReadOnlyCollection<TrendEvaluation>>> LoadEvaluationsAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken)
    {
        var projects = projectIds.ToArray();
        var evaluationRows = await context.Evaluations.AsNoTracking()
            .Where(item => projects.Contains(item.ProjectId) && item.Rubric.ClassId == null &&
                           (item.Status == EvaluationStatus.Submitted || item.Status == EvaluationStatus.Published))
            .Select(item => new
            {
                item.ProjectId,
                item.Id,
                item.RubricId,
                CheckpointId = item.Rubric.CheckpointId,
                item.Status,
                item.TotalScore,
                item.MaxTotalScore,
                UpdatedAt = item.UpdatedAt ?? item.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return evaluationRows
            .GroupBy(item => item.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<TrendEvaluation>)group
                    .Select(item => new TrendEvaluation(
                        item.Id, item.RubricId, item.CheckpointId, item.Status, item.TotalScore, item.MaxTotalScore, item.UpdatedAt))
                    .ToArray());
    }
}
