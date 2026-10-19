using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectDataTrends;

public sealed class GetProjectDataTrendsQueryHandler(IApplicationDbContext context) : IGetProjectDataTrendsQueryHandler
{
    public async Task<Result<ProjectDataTrendsResponse>> HandleAsync(
        GetProjectDataRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Result.Failure<ProjectDataTrendsResponse>(
                ErrorCodes.ProjectDataAccessDenied,
                "Only administrators and lecturers can view project data.");
        }

        if (ProjectDataQuery.Normalize(request.Search)?.Length > ProjectDataQuery.MaxSearchLength)
        {
            return Result.Failure<ProjectDataTrendsResponse>(
                ErrorCodes.ProjectDataValidationError,
                "The search value is invalid.");
        }

        // A trend spans several semesters, so the semester and year filters do not apply. Everything else (scope,
        // subject, group, industry, major, staff, achievement, search) is the same as the table.
        var scope = new GetProjectDataRequest
        {
            Search = request.Search,
            SubjectCode = request.SubjectCode,
            Group = request.Group,
            StartupIndustry = request.StartupIndustry,
            Major = request.Major,
            LecturerId = request.LecturerId,
            MentorId = request.MentorId,
            Achievement = request.Achievement,
        };

        var rows = await ProjectDataQuery.ApplyFilters(
                ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin),
                scope)
            .Select(project => new
            {
                ProjectId = project.Id,
                SemesterCode = project.Team.Class.Semester.Code,
                project.Team.Class.Semester.Year,
                project.Team.Class.Semester.Term,
                CourseId = project.Team.Class.Course.Id,
                SubjectCode = project.Team.Class.Course.Code,
                ClassId = project.Team.Class.Id,
                project.Team.Class.ClassCode,
                project.IsHighPotential,
                project.IsInternallyFunded,
                project.IsExternallyFunded,
                project.IsAwarded,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return Result.Success(new ProjectDataTrendsResponse());

        var courseIds = rows.Select(row => row.CourseId).Distinct().ToArray();
        var projectIds = rows.Select(row => row.ProjectId).ToArray();

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

        var projects = rows.Select(row => new TrendProject(
            row.ProjectId,
            row.SemesterCode,
            row.Year,
            row.Term,
            row.SubjectCode,
            row.ClassId,
            row.ClassCode,
            row.IsHighPotential,
            row.IsInternallyFunded,
            row.IsExternallyFunded,
            row.IsAwarded,
            OfficialCourseScore.Calculate(
                componentsByCourse[row.CourseId],
                evaluationsByProject.GetValueOrDefault(row.ProjectId) ?? []))).ToArray();

        return Result.Success(ProjectDataTrendBuilder.Build(projects));
    }
}
