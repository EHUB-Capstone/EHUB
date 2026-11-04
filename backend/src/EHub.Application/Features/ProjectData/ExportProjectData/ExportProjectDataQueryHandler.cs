using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.ProjectData.Common;
using EHub.Application.Features.ProjectData.GetProjectDataDistribution;
using EHub.Application.Features.ProjectData.GetProjectDataTrends;
using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.ExportProjectData;

public sealed class ExportProjectDataQueryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IExportProjectDataQueryHandler
{
    public async Task<Result<ProjectDataExportFile>> HandleAsync(
        GetProjectDataRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Result.Failure<ProjectDataExportFile>(
                ErrorCodes.ProjectDataAccessDenied,
                "Only administrators and lecturers can export project data.");
        }

        if (ProjectDataQuery.Normalize(request.Search)?.Length > ProjectDataQuery.MaxSearchLength)
        {
            return Result.Failure<ProjectDataExportFile>(
                ErrorCodes.ProjectDataValidationError,
                "The search value is invalid.");
        }

        // Same scope, filters and order as the table, so a lecturer's report never includes classes they do not teach.
        var filtered = ProjectDataQuery.ApplyFilters(
            ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin),
            request);

        var rows = await ProjectDataQuery.ApplySort(filtered, request.SortBy, request.IsDescending)
            .Select(project => new ProjectRow(
                project.Id,
                project.TeamId,
                project.Team.Class.Semester.Code,
                project.Team.Class.Semester.Year,
                project.Team.Class.Semester.Term,
                project.Team.Class.CourseId,
                project.Team.Class.Course.Code,
                project.Team.Class.Id,
                project.Team.Class.ClassCode,
                project.Name,
                project.Description,
                project.Team.Class.PrimaryLecturer == null ? null : project.Team.Class.PrimaryLecturer.FullName,
                project.IsHighPotential,
                project.IsInternallyFunded,
                project.IsExternallyFunded,
                project.IsAwarded,
                project.IsPitching,
                project.IsDemo,
                project.AchievementNote,
                project.AchievementsUpdatedAt,
                project.AchievementsUpdatedBy))
            .ToListAsync(cancellationToken);

        var projects = await BuildProjectsAsync(rows, cancellationToken);

        // The competition sheets list entries, so a competition filter narrows them to the entries that match it.
        var entryFilter = ProjectCompetitionMapping.EntryFilter(request)?.Compile();
        var competitionRows = projects
            .SelectMany(project => project.Competitions
                .Where(entry => entryFilter is null || entryFilter(ToEntry(entry)))
                .Select(entry => new ProjectCompetitionExportRow(
                    entry.Competition,
                    entry.Name,
                    entry.Year,
                    entry.Stage,
                    project.ProjectName,
                    project.SemesterCode,
                    project.SubjectCode,
                    project.ClassCode,
                    project.Groups,
                    project.Lecturer,
                    project.Mentor,
                    project.AcademicMentor)))
            .ToList();

        var exportedBy = await context.Users.AsNoTracking()
            .Where(user => user.Id == currentUserId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var model = new ProjectDataExportModel(
            now,
            string.IsNullOrWhiteSpace(exportedBy) ? string.Empty : exportedBy,
            isAdmin ? "Toàn bộ dự án trong hệ thống" : "Các lớp do giảng viên phụ trách",
            await DescribeFiltersAsync(request, cancellationToken),
            projects,
            competitionRows);

        return Result.Success(new ProjectDataExportFile(
            ProjectDataExportWorkbookBuilder.Build(model),
            ProjectDataExportWorkbookBuilder.ContentType,
            $"project-data-{now:yyyyMMdd}.xlsx"));
    }

    private async Task<IReadOnlyList<ExportProject>> BuildProjectsAsync(
        IReadOnlyList<ProjectRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return [];

        var projectIds = rows.Select(row => row.ProjectId).ToArray();
        var teamIds = rows.Select(row => row.TeamId).Distinct().ToArray();

        var tagRows = await context.ProjectTags.AsNoTracking()
            .Where(tag => projectIds.Contains(tag.ProjectId) && tag.TagType == ProjectTagType.StartupField)
            .Select(tag => new { tag.ProjectId, tag.TagName })
            .ToListAsync(cancellationToken);
        var industriesByProject = tagRows
            .GroupBy(tag => tag.ProjectId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)ProjectDataQuery.DistinctSorted(group.Select(tag => tag.TagName)));

        // One query for the active members: their group names and the majors the distribution counts them under.
        var memberRows = await context.TeamMembers.AsNoTracking()
            .Where(member => teamIds.Contains(member.TeamId) && member.CountsTowardActiveTeam)
            .Select(member => new
            {
                member.TeamId,
                Group = member.ClassStudent.SemesterGroupName,
                Enrollment = member.ClassStudent.MajorCodeAtEnrollment,
                Profile = member.ClassStudent.Student.MajorCode,
            })
            .ToListAsync(cancellationToken);
        var membersByTeam = memberRows
            .GroupBy(member => member.TeamId)
            .ToDictionary(group => group.Key, group => (
                Count: group.Count(),
                Groups: string.Join(", ", ProjectDataQuery.DistinctSortedNaturally(
                    group.Where(member => member.Group != null).Select(member => member.Group!))),
                Majors: (IReadOnlyList<string>)ProjectDataQuery.DistinctSorted(
                    group.Select(member => ProjectDataMajorBucket.Resolve(member.Enrollment, member.Profile)))));

        var mentors = await ProjectDataMentorResolver.LoadAsync(
            context.MentorAssignments.AsNoTracking().Where(assignment => teamIds.Contains(assignment.TeamId)),
            cancellationToken);

        var entryRows = await context.ProjectCompetitionEntries.AsNoTracking()
            .Where(entry => projectIds.Contains(entry.ProjectId))
            .ToListAsync(cancellationToken);
        var competitionsByProject = entryRows
            .GroupBy(entry => entry.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ExportCompetition>)ProjectCompetitionMapping.ToOrderedResponses(group)
                    .Select(item => new ExportCompetition(
                        item.Competition, item.Name, item.Year, ProjectCompetitionMapping.ParseStage(item.Stage)!.Value))
                    .ToArray());

        var updaterIds = rows.Where(row => row.AchievementsUpdatedBy.HasValue)
            .Select(row => row.AchievementsUpdatedBy!.Value).Distinct().ToArray();
        var updaterNames = updaterIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await context.Users.AsNoTracking()
                .Where(user => updaterIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        var scores = await ProjectDataScoreLoader.LoadAsync(
            context, rows.Select(row => (row.ProjectId, row.CourseId)).ToArray(), cancellationToken);

        return rows.Select(row =>
        {
            var members = membersByTeam.GetValueOrDefault(row.TeamId);
            return new ExportProject(
                row.ProjectId,
                row.SemesterCode,
                row.SemesterYear,
                row.Term,
                row.SubjectCode,
                row.ClassId,
                row.ClassCode,
                row.ProjectName,
                row.Description,
                members.Groups ?? string.Empty,
                industriesByProject.GetValueOrDefault(row.ProjectId) ?? [],
                row.LecturerName ?? string.Empty,
                mentors.GetValueOrDefault((row.TeamId, MentorType.Enterprise))?.FullName ?? string.Empty,
                mentors.GetValueOrDefault((row.TeamId, MentorType.Academic))?.FullName ?? string.Empty,
                members.Count,
                // A project without any counting member has no major to report, like the distribution says.
                members.Majors ?? [ProjectDataDistributionRules.UnspecifiedMajor],
                row.IsHighPotential,
                row.IsInternallyFunded,
                row.IsExternallyFunded,
                row.IsAwarded,
                row.IsPitching,
                row.IsDemo,
                row.AchievementNote,
                row.AchievementsUpdatedAt,
                row.AchievementsUpdatedBy is { } updaterId && updaterNames.TryGetValue(updaterId, out var updater) ? updater : null,
                scores.GetValueOrDefault(row.ProjectId),
                competitionsByProject.GetValueOrDefault(row.ProjectId) ?? []);
        }).ToArray();
    }

    /// <summary>The search and filters as readable lines, so the report says which projects it covers.</summary>
    private async Task<IReadOnlyList<string>> DescribeFiltersAsync(GetProjectDataRequest request, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        void Add(string label, IEnumerable<string> values)
        {
            var list = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
            if (list.Length > 0) lines.Add($"{label}: {string.Join(", ", list)}");
        }

        Add("Tìm kiếm", [request.Search?.Trim() ?? string.Empty]);
        Add("Môn", [request.SubjectCode?.Trim() ?? string.Empty]);
        Add("Học kỳ", [ProjectDataQuery.ParseTerm(request.Semester) is { } term
            ? ProjectDataTrendBuilder.TermCode(term)
            : string.Empty]);
        Add("Năm", request.Year is { } year ? [year.ToString()] : []);
        Add("Nhóm", ProjectDataQuery.NormalizeAll(request.Group, value => value));
        Add("Ngành khởi nghiệp", ProjectDataQuery.NormalizeAll(request.StartupIndustry, value => value));
        Add("Chuyên ngành", ProjectDataQuery.NormalizeAll(request.Major, value => value.ToUpperInvariant()));
        Add("Giảng viên", await NamesAsync(ProjectDataQuery.DistinctIds(request.LecturerId), cancellationToken));
        Add("Mentor", await NamesAsync(ProjectDataQuery.DistinctIds(request.MentorId), cancellationToken));
        Add("Thành tích", ProjectDataQuery.NormalizeAll(request.Achievement, value => ProjectAchievementMapping.Canonicalize(value) ?? string.Empty));
        Add("Cuộc thi", ProjectDataQuery.NormalizeAll(request.Competition, value => ProjectCompetitionMapping.Canonicalize(value) ?? string.Empty));
        Add("Vòng thi", ProjectDataQuery.NormalizeAll(request.CompetitionStage, value => ProjectCompetitionMapping.ParseStage(value)?.ToString() ?? string.Empty));
        Add("Năm thi", (request.CompetitionYear ?? []).Distinct().Select(item => item.ToString()));
        return lines;
    }

    private async Task<IEnumerable<string>> NamesAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0) return [];
        var names = await context.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
        return ids.Select(id => names.GetValueOrDefault(id) ?? id.ToString());
    }

    private static Domain.Entities.ProjectCompetitionEntry ToEntry(ExportCompetition entry) =>
        new() { Competition = entry.Competition, Year = entry.Year, Stage = entry.Stage };

    private sealed record ProjectRow(
        Guid ProjectId,
        Guid TeamId,
        string SemesterCode,
        int SemesterYear,
        SemesterTerm Term,
        Guid CourseId,
        string SubjectCode,
        Guid ClassId,
        string ClassCode,
        string ProjectName,
        string? Description,
        string? LecturerName,
        bool IsHighPotential,
        bool IsInternallyFunded,
        bool IsExternallyFunded,
        bool IsAwarded,
        bool IsPitching,
        bool IsDemo,
        string? AchievementNote,
        DateTime? AchievementsUpdatedAt,
        Guid? AchievementsUpdatedBy);
}
