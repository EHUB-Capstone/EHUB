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
            Competition = request.Competition,
            CompetitionStage = request.CompetitionStage,
            CompetitionYear = request.CompetitionYear,
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
                project.IsPitching,
                project.IsDemo,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0) return Result.Success(new ProjectDataTrendsResponse());

        var scores = await ProjectDataScoreLoader.LoadAsync(
            context, rows.Select(row => (row.ProjectId, row.CourseId)).ToArray(), cancellationToken);

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
            row.IsPitching,
            row.IsDemo,
            scores.GetValueOrDefault(row.ProjectId))).ToArray();

        return Result.Success(ProjectDataTrendBuilder.Build(projects));
    }
}
