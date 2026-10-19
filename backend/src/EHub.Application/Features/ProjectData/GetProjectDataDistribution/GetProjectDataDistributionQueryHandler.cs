using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectDataDistribution;

public sealed class GetProjectDataDistributionQueryHandler(IApplicationDbContext context) : IGetProjectDataDistributionQueryHandler
{
    public async Task<Result<ProjectDataDistributionResponse>> HandleAsync(
        GetProjectDataRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Result.Failure<ProjectDataDistributionResponse>(
                ErrorCodes.ProjectDataAccessDenied,
                "Only administrators and lecturers can view project data.");
        }

        if (ProjectDataQuery.Normalize(request.Search)?.Length > ProjectDataQuery.MaxSearchLength)
        {
            return Result.Failure<ProjectDataDistributionResponse>(
                ErrorCodes.ProjectDataValidationError,
                "The search value is invalid.");
        }

        // Same scope and filters as the list and the summary, so every bucket describes rows the table can show.
        var projects = ProjectDataQuery.ApplyFilters(
            ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin),
            request);

        var outcomes = await projects
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Potential = group.Count(project => project.IsHighPotential),
                InternallyFunded = group.Count(project => project.IsInternallyFunded),
                ExternallyFunded = group.Count(project => project.IsExternallyFunded),
                Awarded = group.Count(project => project.IsAwarded),
                None = group.Count(project => !project.IsHighPotential && !project.IsInternallyFunded && !project.IsExternallyFunded && !project.IsAwarded),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var semesterRows = await projects
            .GroupBy(project => new
            {
                project.Team.Class.Semester.Code,
                project.Team.Class.Semester.Year,
                project.Team.Class.Semester.Term,
            })
            .Select(group => new { group.Key.Code, group.Key.Year, group.Key.Term, Count = group.Count() })
            .ToListAsync(cancellationToken);

        // One tag row per project per industry (unique index), so a plain count is a project count.
        var industryRows = await projects
            .SelectMany(project => project.ProjectTags
                .Where(tag => tag.TagType == ProjectTagType.StartupField)
                .Select(tag => new { tag.NormalizedTagName, tag.TagName }))
            .GroupBy(tag => tag.NormalizedTagName)
            .Select(group => new { Name = group.Min(tag => tag.TagName)!, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var withoutIndustry = await projects.CountAsync(
            project => !project.ProjectTags.Any(tag => tag.TagType == ProjectTagType.StartupField),
            cancellationToken);

        var memberRows = await projects
            .SelectMany(project => project.Team.TeamMembers
                .Where(member => member.CountsTowardActiveTeam)
                .Select(member => new
                {
                    ProjectId = project.Id,
                    Enrollment = member.ClassStudent.MajorCodeAtEnrollment,
                    Profile = member.ClassStudent.Student.MajorCode,
                }))
            .ToListAsync(cancellationToken);

        var total = outcomes?.Total ?? 0;
        var majorCounts = memberRows
            .Select(row => (row.ProjectId, Major: ProjectDataMajorBucket.Resolve(row.Enrollment, row.Profile)))
            .Distinct()
            .GroupBy(pair => pair.Major)
            .ToDictionary(group => group.Key, group => group.Count());

        // A project without any counting member has no major to report.
        var withoutMembers = total - memberRows.Select(row => row.ProjectId).Distinct().Count();
        if (withoutMembers > 0)
        {
            majorCounts[ProjectDataDistributionRules.UnspecifiedMajor] =
                majorCounts.GetValueOrDefault(ProjectDataDistributionRules.UnspecifiedMajor) + withoutMembers;
        }

        return Result.Success(new ProjectDataDistributionResponse
        {
            TotalGroups = total,
            Semesters = semesterRows
                .OrderBy(row => row.Year)
                .ThenBy(row => row.Term)
                .Select(row => new ProjectDataDistributionSemesterResponse
                {
                    Code = row.Code,
                    Year = row.Year,
                    Term = row.Term switch
                    {
                        SemesterTerm.Spring => "SP",
                        SemesterTerm.Summer => "SU",
                        _ => "FA",
                    },
                    Count = row.Count,
                })
                .ToArray(),
            Majors = majorCounts
                .OrderBy(pair => pair.Key == ProjectDataDistributionRules.UnspecifiedMajor)
                .ThenByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ProjectDataDistributionBucketResponse { Name = pair.Key, Count = pair.Value })
                .ToArray(),
            StartupIndustries = industryRows
                .OrderByDescending(row => row.Count)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .Select(row => new ProjectDataDistributionBucketResponse { Name = row.Name, Count = row.Count })
                .ToArray(),
            UnspecifiedStartupIndustryCount = withoutIndustry,
            Outcomes =
            [
                new() { Name = ProjectAchievementNames.Potential, Count = outcomes?.Potential ?? 0 },
                new() { Name = ProjectAchievementNames.InternallyFunded, Count = outcomes?.InternallyFunded ?? 0 },
                new() { Name = ProjectAchievementNames.ExternallyFunded, Count = outcomes?.ExternallyFunded ?? 0 },
                new() { Name = ProjectAchievementNames.Awarded, Count = outcomes?.Awarded ?? 0 },
            ],
            NoOutcomeCount = outcomes?.None ?? 0,
        });
    }
}
