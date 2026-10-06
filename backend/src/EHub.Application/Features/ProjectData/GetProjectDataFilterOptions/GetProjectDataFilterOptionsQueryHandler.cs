using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectDataFilterOptions;

public sealed class GetProjectDataFilterOptionsQueryHandler(IApplicationDbContext context)
    : IGetProjectDataFilterOptionsQueryHandler
{
    public async Task<Result<ProjectDataFilterOptionsResponse>> HandleAsync(
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Result.Failure<ProjectDataFilterOptionsResponse>(
                ErrorCodes.ProjectDataAccessDenied,
                "Only administrators and lecturers can view project data filters.");
        }

        // Options come from the same scope as the list, independent of the current page or filters.
        var scoped = ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin);

        var years = await scoped
            .Select(project => project.Team.Class.Semester.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync(cancellationToken);

        var subjects = await scoped
            .Select(project => new ProjectDataSubjectOptionResponse
            {
                Code = project.Team.Class.Course.Code,
                Name = project.Team.Class.Course.Name,
            })
            .Distinct()
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);

        var groupNames = await scoped
            .SelectMany(project => project.Team.TeamMembers)
            .Where(member => member.CountsTowardActiveTeam && member.ClassStudent.SemesterGroupName != null)
            .Select(member => member.ClassStudent.SemesterGroupName!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var industryNames = await scoped
            .SelectMany(project => project.ProjectTags)
            .Where(tag => tag.TagType == ProjectTagType.StartupField)
            .Select(tag => tag.TagName)
            .Distinct()
            .ToListAsync(cancellationToken);

        var lecturers = await scoped
            .Where(project => project.Team.Class.PrimaryLecturer != null)
            .Select(project => new ProjectDataPersonResponse
            {
                UserId = project.Team.Class.PrimaryLecturer!.Id,
                FullName = project.Team.Class.PrimaryLecturer.FullName,
            })
            .Distinct()
            .OrderBy(item => item.FullName)
            .ToListAsync(cancellationToken);

        var scopedTeamIds = scoped.Select(project => project.TeamId);
        var mentorRows = await ProjectDataMentorResolver.LoadAsync(
            context.MentorAssignments.AsNoTracking().Where(assignment => scopedTeamIds.Contains(assignment.TeamId)),
            cancellationToken);
        var mentors = mentorRows.Values
            .Select(row => new ProjectDataMentorOptionResponse
            {
                UserId = row.UserId,
                FullName = row.FullName,
                Slot = row.Slot.ToString(),
            })
            .DistinctBy(item => (item.UserId, item.Slot))
            .OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Slot, StringComparer.Ordinal)
            .ToArray();

        return Result.Success(new ProjectDataFilterOptionsResponse
        {
            Subjects = subjects,
            Years = years,
            Groups = ProjectDataQuery.DistinctSortedNaturally(groupNames),
            StartupIndustries = ProjectDataQuery.DistinctSorted(industryNames),
            Lecturers = lecturers,
            Mentors = mentors,
        });
    }

}
