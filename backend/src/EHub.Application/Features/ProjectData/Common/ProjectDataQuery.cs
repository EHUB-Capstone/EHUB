using System.Linq.Expressions;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;

namespace EHub.Application.Features.ProjectData.Common;

internal static class ProjectDataQuery
{
    internal const int MaxSearchLength = 100;
    internal const int MaxPageSize = 100;

    internal static readonly int[] AllowedPageSizes = [10, 20, 50, 100];

    internal const string SortByProjectName = "projectName";
    internal const string SortBySemester = "semester";
    internal const string SortByClassCode = "classCode";
    internal const string SortByGroup = "group";

    internal static readonly string[] AllowedSortFields = [SortByClassCode, SortBySemester, SortByGroup, SortByProjectName];

    internal static bool IsAdmin(string role) =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);

    internal static bool IsLecturer(string role) =>
        string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Projects visible to the feature: live (not archived) projects of active teams, in classes that are
    /// running or already completed. A class archived after completion stays visible as historical data.
    /// Soft-deleted rows are excluded by the global query filters. Lecturers only see classes they teach.
    /// </summary>
    internal static IQueryable<Project> Scoped(IQueryable<Project> projects, Guid userId, bool isAdmin)
    {
        var query = projects.Where(project =>
            project.Status != ProjectStatus.Archived &&
            project.Team.Status == TeamStatus.Active &&
            (project.Team.Class.Status == ClassStatus.Completed ||
             project.Team.Class.Status == ClassStatus.Archived &&
             project.Team.Class.StatusBeforeArchive == ClassStatus.Completed ||
             project.Team.Class.Status != ClassStatus.Archived &&
             project.Team.Class.Semester.Status != SemesterStatus.Archived));

        return isAdmin ? query : query.Where(TaughtBy(userId));
    }

    internal static IQueryable<Project> ApplyFilters(IQueryable<Project> query, GetProjectDataRequest request)
    {
        if (ParseTerm(request.Semester) is { } term)
            query = query.Where(project => project.Team.Class.Semester.Term == term);

        if (request.Year is { } year)
            query = query.Where(project => project.Team.Class.Semester.Year == year);

        if (Normalize(request.SubjectCode) is { } subjectCode)
        {
            var code = subjectCode.ToLower();
            query = query.Where(project => project.Team.Class.Course.Code.ToLower() == code);
        }

        if (Normalize(request.Group) is { } group)
        {
            var normalizedGroup = group.ToLower();
            query = query.Where(project => project.Team.TeamMembers.Any(member =>
                member.CountsTowardActiveTeam &&
                member.ClassStudent.SemesterGroupName != null &&
                member.ClassStudent.SemesterGroupName.Trim().ToLower() == normalizedGroup));
        }

        if (Normalize(request.StartupIndustry) is { } industry)
        {
            var normalizedIndustry = industry.ToLower();
            query = query.Where(project => project.ProjectTags.Any(tag =>
                tag.TagType == ProjectTagType.StartupField &&
                tag.TagName.ToLower() == normalizedIndustry));
        }

        if (request.LecturerId is { } lecturerId)
            query = query.Where(TaughtBy(lecturerId));

        if (request.MentorId is { } mentorId)
            query = query.Where(ProjectDataMentorResolver.HasDisplayedMentor(mentorId));

        if (Normalize(request.Achievement) is { } achievement)
        {
            query = ProjectAchievementMapping.Canonicalize(achievement) switch
            {
                ProjectAchievementNames.Potential => query.Where(project => project.IsHighPotential),
                ProjectAchievementNames.Funded => query.Where(project => project.IsFunded),
                ProjectAchievementNames.Awarded => query.Where(project => project.IsAwarded),
                _ => query,
            };
        }

        if (Normalize(request.Search) is { } search)
            query = ApplySearch(query, search.ToLower());

        return query;
    }

    /// <summary>
    /// Default order is class code (course code, then the numeric class index, so EXE101_2 precedes EXE101_10),
    /// then semester, then the team's group, then project name; Id keeps it stable. A team's group key is its
    /// shortest-then-lowest group code, which orders the numeric suffix naturally (G2 before G10) within a class;
    /// teams without a group always come last. When sorting descending only the primary keys are reversed.
    /// </summary>
    internal static IQueryable<Project> ApplySort(IQueryable<Project> query, string? sortBy, bool isDescending)
    {
        var field = AllowedSortFields.FirstOrDefault(item =>
            string.Equals(item, sortBy, StringComparison.OrdinalIgnoreCase)) ?? SortByClassCode;

        return field switch
        {
            SortByProjectName => ThenId(isDescending
                ? query.OrderByDescending(project => project.Name)
                : query.OrderBy(project => project.Name)),
            SortBySemester => ThenName(ThenGroup(ThenClass(isDescending
                ? query.OrderByDescending(project => project.Team.Class.Semester.Year)
                    .ThenByDescending(project =>
                        project.Team.Class.Semester.Term == SemesterTerm.Spring ? 0
                        : project.Team.Class.Semester.Term == SemesterTerm.Summer ? 1
                        : 2)
                : query.OrderBy(project => project.Team.Class.Semester.Year)
                    .ThenBy(project =>
                        project.Team.Class.Semester.Term == SemesterTerm.Spring ? 0
                        : project.Team.Class.Semester.Term == SemesterTerm.Summer ? 1
                        : 2)))),
            SortByGroup => ThenName(ThenSemester(ThenClass(isDescending
                ? query.OrderBy(WithGroupKey(key => key == null))
                    .ThenByDescending(WithGroupKey(key => key!.Length))
                    .ThenByDescending(WithGroupKey(key => key))
                : query.OrderBy(WithGroupKey(key => key == null))
                    .ThenBy(WithGroupKey(key => key!.Length))
                    .ThenBy(WithGroupKey(key => key))))),
            _ => ThenName(ThenGroup(ThenSemester(isDescending
                ? query.OrderByDescending(project => project.Team.Class.Course.Code)
                    .ThenByDescending(project => project.Team.Class.ClassIndex)
                : query.OrderBy(project => project.Team.Class.Course.Code)
                    .ThenBy(project => project.Team.Class.ClassIndex)))),
        };
    }

    /// <summary>The team's shortest-then-lowest non-blank group code, or null when no member has a group.</summary>
    private static readonly Expression<Func<Project, string?>> GroupKey = project =>
        project.Team.TeamMembers
            .Where(member => member.CountsTowardActiveTeam &&
                             member.ClassStudent.SemesterGroupName != null &&
                             member.ClassStudent.SemesterGroupName.Trim() != "")
            .Select(member => member.ClassStudent.SemesterGroupName!.Trim())
            .OrderBy(group => group.Length)
            .ThenBy(group => group)
            .FirstOrDefault();

    /// <summary>Builds a project-level sort key from the group key so EF can translate it inline.</summary>
    private static Expression<Func<Project, TKey>> WithGroupKey<TKey>(Expression<Func<string?, TKey>> keySelector)
    {
        var body = new ParameterSubstitution(keySelector.Parameters[0], GroupKey.Body).Visit(keySelector.Body);
        return Expression.Lambda<Func<Project, TKey>>(body, GroupKey.Parameters[0]);
    }

    private sealed class ParameterSubstitution(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == parameter ? replacement : base.VisitParameter(node);
    }

    private static IOrderedQueryable<Project> ThenClass(IOrderedQueryable<Project> query) =>
        query.ThenBy(project => project.Team.Class.Course.Code)
            .ThenBy(project => project.Team.Class.ClassIndex);

    private static IOrderedQueryable<Project> ThenSemester(IOrderedQueryable<Project> query) =>
        query.ThenBy(project => project.Team.Class.Semester.Year)
            .ThenBy(project =>
                project.Team.Class.Semester.Term == SemesterTerm.Spring ? 0
                : project.Team.Class.Semester.Term == SemesterTerm.Summer ? 1
                : 2);

    private static IOrderedQueryable<Project> ThenGroup(IOrderedQueryable<Project> query) =>
        query.ThenBy(WithGroupKey(key => key == null))
            .ThenBy(WithGroupKey(key => key!.Length))
            .ThenBy(WithGroupKey(key => key));

    private static IOrderedQueryable<Project> ThenName(IOrderedQueryable<Project> query) =>
        ThenId(query.ThenBy(project => project.Name));

    private static IOrderedQueryable<Project> ThenId(IOrderedQueryable<Project> query) =>
        query.ThenBy(project => project.Id);

    /// <summary>Trims, drops blanks and merges case-insensitive duplicates, keeping one deterministic spelling.</summary>
    internal static string[] DistinctSorted(IEnumerable<string> values) =>
        values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Order(StringComparer.Ordinal)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>Like <see cref="DistinctSorted"/> but orders digit runs numerically, so G2 comes before G10.</summary>
    internal static string[] DistinctSortedNaturally(IEnumerable<string> values) =>
        values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Order(StringComparer.Ordinal)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(NaturalStringComparer.Instance)
            .ToArray();

    /// <summary>Maps the SP/SU/FA short codes used across the app to the semester term, or null when unknown.</summary>
    internal static SemesterTerm? ParseTerm(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "SP" => SemesterTerm.Spring,
        "SU" => SemesterTerm.Summer,
        "FA" => SemesterTerm.Fall,
        _ => null,
    };

    internal static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static IQueryable<Project> ApplySearch(IQueryable<Project> query, string term)
    {
        Expression<Func<Project, bool>> projectFields = project =>
            project.Name.ToLower().Contains(term) ||
            project.Description != null && project.Description.ToLower().Contains(term) ||
            project.Team.Class.Course.Code.ToLower().Contains(term) ||
            project.Team.Class.ClassCode.ToLower().Contains(term) ||
            project.Team.Class.Semester.Code.ToLower().Contains(term) ||
            project.Team.Class.PrimaryLecturer != null &&
            project.Team.Class.PrimaryLecturer.FullName.ToLower().Contains(term) ||
            project.ProjectTags.Any(tag =>
                tag.TagType == ProjectTagType.StartupField && tag.TagName.ToLower().Contains(term));

        return query.Where(ProjectDataExpressions.Or(
            projectFields,
            ProjectDataMentorResolver.HasDisplayedMentorNamed(term)));
    }

    /// <summary>The lecturer is the primary lecturer of the class or one of its assigned lecturers.</summary>
    private static Expression<Func<Project, bool>> TaughtBy(Guid lecturerId) =>
        project =>
            project.Team.Class.PrimaryLecturerId == lecturerId ||
            project.Team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == lecturerId);
}
