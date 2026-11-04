using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectDataTrends;

/// <summary>
/// Loads the official 0-10 course score of projects (see <see cref="OfficialCourseScore"/>). The trends and the
/// project data export both use it, so a score never differs between the two.
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

        // Course-level parts of the score. Checkpoints and assessments together carry the 100% weight.
        var checkpointComponents = await context.Checkpoints.AsNoTracking()
            .Where(item => item.CourseId.HasValue && courseIds.Contains(item.CourseId.Value) &&
                           item.ClassId == null && item.Status != CheckpointStatus.Archived && item.CourseWeight > 0)
            .Select(item => new { CourseId = item.CourseId!.Value, item.Id, item.CourseWeight })
            .ToListAsync(cancellationToken);
        var assessmentComponents = await context.Rubrics.AsNoTracking()
            .Where(item => item.CourseId.HasValue && courseIds.Contains(item.CourseId.Value) &&
                           item.ClassId == null && item.CheckpointId == null &&
                           item.Status == RubricStatus.Active && item.CourseWeight > 0)
            .Select(item => new { CourseId = item.CourseId!.Value, item.Id, item.CourseWeight })
            .ToListAsync(cancellationToken);
        var componentsByCourse = courseIds.ToDictionary(
            courseId => courseId,
            courseId => (IReadOnlyCollection<TrendComponent>)
            [
                .. checkpointComponents.Where(item => item.CourseId == courseId)
                    .Select(item => new TrendComponent(item.Id, null, item.CourseWeight)),
                .. assessmentComponents.Where(item => item.CourseId == courseId)
                    .Select(item => new TrendComponent(null, item.Id, item.CourseWeight)),
            ]);

        // Only course-level rubrics, whatever their status now, so a semester keeps its score after a rubric is replaced.
        var evaluationRows = await context.Evaluations.AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId) && item.Rubric.ClassId == null &&
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
        var evaluationsByProject = evaluationRows
            .GroupBy(item => item.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<TrendEvaluation>)group
                    .Select(item => new TrendEvaluation(
                        item.Id, item.RubricId, item.CheckpointId, item.Status, item.TotalScore, item.MaxTotalScore, item.UpdatedAt))
                    .ToArray());

        return projects
            .DistinctBy(project => project.ProjectId)
            .ToDictionary(
                project => project.ProjectId,
                project => OfficialCourseScore.Calculate(
                    componentsByCourse[project.CourseId],
                    evaluationsByProject.GetValueOrDefault(project.ProjectId) ?? []));
    }
}
