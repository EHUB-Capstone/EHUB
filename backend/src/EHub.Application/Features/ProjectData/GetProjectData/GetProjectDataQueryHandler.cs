using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.GetProjectData;

public sealed class GetProjectDataQueryHandler(IApplicationDbContext context) : IGetProjectDataQueryHandler
{
    public async Task<Result<PagedResponse<ProjectDataItemResponse>>> HandleAsync(
        GetProjectDataRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Result.Failure<PagedResponse<ProjectDataItemResponse>>(
                ErrorCodes.ProjectDataAccessDenied,
                "Only administrators and lecturers can view project data.");
        }

        if (request.PageIndex < 1 ||
            !ProjectDataQuery.AllowedPageSizes.Contains(request.PageSize) ||
            ProjectDataQuery.Normalize(request.Search)?.Length > ProjectDataQuery.MaxSearchLength)
        {
            return Result.Failure<PagedResponse<ProjectDataItemResponse>>(
                ErrorCodes.ProjectDataValidationError,
                "The page, page size or search value is invalid.");
        }

        // Scope first so that counts, search, filters and paging never touch out-of-scope projects.
        var filtered = ProjectDataQuery.ApplyFilters(
            ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin),
            request);

        var totalItems = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)request.PageSize);

        var rows = await ProjectDataQuery.ApplySort(filtered, request.SortBy, request.IsDescending)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(project => new ProjectRow(
                project.Id,
                project.TeamId,
                project.Team.ClassId,
                project.Team.Class.SemesterId,
                project.Team.Class.Semester.Code,
                project.Team.Class.CourseId,
                project.Team.Class.Course.Code,
                project.Team.Class.ClassCode,
                project.Name,
                project.Description,
                project.Team.Class.PrimaryLecturerId,
                project.Team.Class.PrimaryLecturer == null ? null : project.Team.Class.PrimaryLecturer.FullName,
                project.IsHighPotential,
                project.IsFunded,
                project.IsAwarded,
                project.AchievementNote,
                project.AchievementsUpdatedAt,
                project.AchievementsUpdatedBy,
                project.Version))
            .ToListAsync(cancellationToken);

        var items = rows.Count == 0
            ? []
            : await BuildItemsAsync(rows, cancellationToken);

        return Result.Success(new PagedResponse<ProjectDataItemResponse>
        {
            Items = items,
            PageIndex = request.PageIndex,
            PageSize = request.PageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
        });
    }

    private async Task<IReadOnlyList<ProjectDataItemResponse>> BuildItemsAsync(
        IReadOnlyList<ProjectRow> rows,
        CancellationToken cancellationToken)
    {
        var projectIds = rows.Select(row => row.ProjectId).ToArray();
        var teamIds = rows.Select(row => row.TeamId).ToArray();

        var tagRows = await context.ProjectTags.AsNoTracking()
            .Where(tag => projectIds.Contains(tag.ProjectId) && tag.TagType == ProjectTagType.StartupField)
            .Select(tag => new { tag.ProjectId, tag.TagName })
            .ToListAsync(cancellationToken);
        var industriesByProject = tagRows
            .GroupBy(tag => tag.ProjectId)
            .ToDictionary(group => group.Key, group => ProjectDataQuery.DistinctSorted(group.Select(tag => tag.TagName)));

        var groupRows = await context.TeamMembers.AsNoTracking()
            .Where(member => teamIds.Contains(member.TeamId) &&
                             member.CountsTowardActiveTeam &&
                             member.ClassStudent.SemesterGroupName != null)
            .Select(member => new { member.TeamId, Group = member.ClassStudent.SemesterGroupName! })
            .Distinct()
            .ToListAsync(cancellationToken);
        var groupsByTeam = groupRows
            .GroupBy(item => item.TeamId)
            .ToDictionary(group => group.Key, group => ProjectDataQuery.DistinctSortedNaturally(group.Select(item => item.Group)));

        var updaterIds = rows.Where(row => row.AchievementsUpdatedBy.HasValue)
            .Select(row => row.AchievementsUpdatedBy!.Value).Distinct().ToArray();
        var updaterNames = updaterIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await context.Users.AsNoTracking()
                .Where(user => updaterIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        var mentors = await ProjectDataMentorResolver.LoadAsync(
            context.MentorAssignments.AsNoTracking().Where(assignment => teamIds.Contains(assignment.TeamId)),
            cancellationToken);

        return rows.Select(row => new ProjectDataItemResponse
        {
            ProjectId = row.ProjectId,
            TeamId = row.TeamId,
            ClassId = row.ClassId,
            SemesterId = row.SemesterId,
            SemesterCode = row.SemesterCode,
            SubjectId = row.SubjectId,
            SubjectCode = row.SubjectCode,
            ClassCode = row.ClassCode,
            Groups = groupsByTeam.GetValueOrDefault(row.TeamId) ?? [],
            ProjectName = row.ProjectName,
            Description = row.Description,
            StartupIndustries = industriesByProject.GetValueOrDefault(row.ProjectId) ?? [],
            Lecturer = row.LecturerId is { } lecturerId && row.LecturerName is { } lecturerName
                ? new ProjectDataPersonResponse { UserId = lecturerId, FullName = lecturerName }
                : null,
            Mentor = ToMentor(mentors.GetValueOrDefault((row.TeamId, MentorType.Enterprise))),
            AcademicMentor = ToMentor(mentors.GetValueOrDefault((row.TeamId, MentorType.Academic))),
            Achievements = ProjectAchievementMapping.ToNames(row.IsHighPotential, row.IsFunded, row.IsAwarded),
            AchievementNote = row.AchievementNote,
            AchievementsUpdatedAtUtc = row.AchievementsUpdatedAt,
            AchievementsUpdatedBy = row.AchievementsUpdatedBy is { } updaterId && updaterNames.TryGetValue(updaterId, out var updaterName)
                ? new ProjectDataPersonResponse { UserId = updaterId, FullName = updaterName }
                : null,
            RowVersion = row.Version.ToString(),
        }).ToArray();
    }

    private static ProjectDataMentorResponse? ToMentor(ProjectDataMentorRow? row) =>
        row is null
            ? null
            : new ProjectDataMentorResponse
            {
                AssignmentId = row.AssignmentId,
                MentorProfileId = row.MentorProfileId,
                UserId = row.UserId,
                FullName = row.FullName,
                Slot = row.Slot.ToString(),
                AssignedAtUtc = row.AssignedAtUtc,
                EndedAtUtc = row.EndedAtUtc,
                IsHistorical = row.Status == MentorAssignmentStatus.Ended,
            };


    private sealed record ProjectRow(
        Guid ProjectId,
        Guid TeamId,
        Guid ClassId,
        Guid SemesterId,
        string SemesterCode,
        Guid SubjectId,
        string SubjectCode,
        string ClassCode,
        string ProjectName,
        string? Description,
        Guid? LecturerId,
        string? LecturerName,
        bool IsHighPotential,
        bool IsFunded,
        bool IsAwarded,
        string? AchievementNote,
        DateTime? AchievementsUpdatedAt,
        Guid? AchievementsUpdatedBy,
        uint Version);
}
