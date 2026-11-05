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

        // Every multi-value filter below keeps a project when ANY of its selected values matches.
        if (NormalizeAll(request.Group, value => value.ToLower()) is { Length: > 0 } groups)
        {
            query = query.Where(project => project.Team.TeamMembers.Any(member =>
                member.CountsTowardActiveTeam &&
                member.ClassStudent.SemesterGroupName != null &&
                groups.Contains(member.ClassStudent.SemesterGroupName.Trim().ToLower())));
        }

        if (NormalizeAll(request.StartupIndustry, value => value.ToLower()) is { Length: > 0 } industries)
        {
            query = query.Where(project => project.ProjectTags.Any(tag =>
                tag.TagType == ProjectTagType.StartupField &&
                industries.Contains(tag.TagName.ToLower())));
        }

        if (DistinctIds(request.LecturerId) is { Length: > 0 } lecturerIds)
            query = query.Where(TaughtByAny(lecturerIds));

        if (DistinctIds(request.MentorId) is { Length: > 0 } mentorIds)
            query = query.Where(ProjectDataMentorResolver.HasDisplayedMentorAny(mentorIds));

        var labels = NormalizeAll(request.Achievement, value => ProjectAchievementMapping.Canonicalize(value) ?? "");
        if (labels.Any(label => label.Length > 0))
        {
            var potential = labels.Contains(ProjectAchievementNames.Potential);
            var internallyFunded = labels.Contains(ProjectAchievementNames.InternallyFunded);
            var externallyFunded = labels.Contains(ProjectAchievementNames.ExternallyFunded);
            var awarded = labels.Contains(ProjectAchievementNames.Awarded);
            var pitching = labels.Contains(ProjectAchievementNames.Pitching);
            var demo = labels.Contains(ProjectAchievementNames.Demo);
            query = query.Where(project =>
                potential && project.IsHighPotential ||
                internallyFunded && project.IsInternallyFunded ||
                externallyFunded && project.IsExternallyFunded ||
                awarded && project.IsAwarded ||
                pitching && project.IsPitching ||
                demo && project.IsDemo);
        }

        if (NormalizeAll(request.Major, value => value.ToUpperInvariant()) is { Length: > 0 } majors)
            query = ApplyMajorFilter(query, majors);

        // The competition filters describe one entry (e.g. FShark + Final + 2026), so they are checked together.
        if (ProjectCompetitionMapping.EntryFilter(request) is { } competitionEntry)
            query = query.Where(project => project.ProjectCompetitionEntries.AsQueryable().Any(competitionEntry));

        if (Normalize(request.Search) is { } search)
            query = ApplySearch(query, search.ToLower());

        return query;
    }

    /// <summary>Every major a filter can name: the real majors plus the bucket for missing or invalid ones.</summary>
    internal static readonly string[] MajorFilterOptions = [.. MajorCodes.All, ProjectDataDistributionRules.UnspecifiedMajor];

    internal static bool IsMajorFilterValue(string? value) =>
        Normalize(value)?.ToUpperInvariant() is { } major &&
        (major == ProjectDataDistributionRules.UnspecifiedMajor || MajorCodes.IsValid(major));

    /// <summary>
    /// Keeps projects with at least one active team member whose effective major is any of <paramref name="majors"/>
    /// (upper-case codes). The effective major is the enrollment major, or the profile major when the enrollment one
    /// is blank or UNDECLARED (<c>StudentEnrollmentRules.ResolveEffectiveMajorCode</c>), written inline because EF
    /// cannot translate that method. UNSPECIFIED also keeps projects with no active members, like the distribution does.
    /// </summary>
    private static IQueryable<Project> ApplyMajorFilter(IQueryable<Project> query, string[] majors)
    {
        const string undeclared = "UNDECLARED";

        var codes = majors.Where(major => major != ProjectDataDistributionRules.UnspecifiedMajor).ToArray();
        var includesUnspecified = codes.Length != majors.Length;

        Expression<Func<Project, bool>> hasCode = project => project.Team.TeamMembers.Any(member =>
            member.CountsTowardActiveTeam &&
            (codes.Contains(member.ClassStudent.MajorCodeAtEnrollment.Trim().ToUpper()) ||
             ((member.ClassStudent.MajorCodeAtEnrollment.Trim().ToUpper() == "" ||
               member.ClassStudent.MajorCodeAtEnrollment.Trim().ToUpper() == undeclared) &&
              member.ClassStudent.Student.MajorCode != null &&
              codes.Contains(member.ClassStudent.Student.MajorCode.Trim().ToUpper()))));

        if (!includesUnspecified) return query.Where(hasCode);

        var valid = MajorCodes.All;
        Expression<Func<Project, bool>> isUnspecified = project =>
            !project.Team.TeamMembers.Any(member => member.CountsTowardActiveTeam) ||
            project.Team.TeamMembers.Any(member => member.CountsTowardActiveTeam &&
                !(valid.Contains(member.ClassStudent.MajorCodeAtEnrollment.Trim().ToUpper()) ||
                  ((member.ClassStudent.MajorCodeAtEnrollment.Trim().ToUpper() == "" ||
                    member.ClassStudent.MajorCodeAtEnrollment.Trim().ToUpper() == undeclared) &&
                   member.ClassStudent.Student.MajorCode != null &&
                   valid.Contains(member.ClassStudent.Student.MajorCode.Trim().ToUpper()))));

        return query.Where(codes.Length == 0 ? isUnspecified : ProjectDataExpressions.Or(hasCode, isUnspecified));
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

    /// <summary>Most values one multi-select filter may carry; keeps the generated SQL and the URL bounded.</summary>
    internal const int MaxFilterValues = 100;

    /// <summary>Trims, drops blanks, applies <paramref name="transform"/> and removes duplicates; empty when nothing is set.</summary>
    internal static string[] NormalizeAll(IEnumerable<string>? values, Func<string, string> transform) =>
        values is null
            ? []
            : values
                .Select(Normalize)
                .OfType<string>()
                .Select(transform)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    internal static Guid[] DistinctIds(IEnumerable<Guid>? ids) => ids?.Distinct().ToArray() ?? [];

    private static IQueryable<Project> ApplySearch(IQueryable<Project> query, string term)
    {
        Expression<Func<Project, bool>> projectFields = project =>
            project.Name.ToLower().Contains(term) ||
            project.Description != null && project.Description.ToLower().Contains(term) ||
            project.Team.Class.Course.Code.ToLower().Contains(term) ||
            project.Team.Class.ClassCode.ToLower().Contains(term) ||
            project.AchievementNote != null && project.AchievementNote.ToLower().Contains(term) ||
            project.Team.Class.Semester.Code.ToLower().Contains(term) ||
            project.Team.Class.PrimaryLecturer != null &&
            project.Team.Class.PrimaryLecturer.FullName.ToLower().Contains(term) ||
            project.ProjectTags.Any(tag =>
                tag.TagType == ProjectTagType.StartupField && tag.TagName.ToLower().Contains(term)) ||
            project.ProjectCompetitionEntries.Any(entry =>
                entry.Competition.ToLower().Contains(term) ||
                entry.OtherName != null && entry.OtherName.ToLower().Contains(term));

        return query.Where(ProjectDataExpressions.Or(
            projectFields,
            ProjectDataMentorResolver.HasDisplayedMentorNamed(term)));
    }

    /// <summary>The lecturer is the primary lecturer of the class or one of its assigned lecturers.</summary>
    private static Expression<Func<Project, bool>> TaughtBy(Guid lecturerId) =>
        project =>
            project.Team.Class.PrimaryLecturerId == lecturerId ||
            project.Team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == lecturerId);

    /// <summary>Any of the lecturers is the primary lecturer of the class or one of its assigned lecturers.</summary>
    private static Expression<Func<Project, bool>> TaughtByAny(Guid[] lecturerIds)
    {
        var ids = lecturerIds.Select(id => (Guid?)id).ToArray();
        return project =>
            ids.Contains(project.Team.Class.PrimaryLecturerId) ||
            project.Team.Class.ClassLecturers.Any(assignment => lecturerIds.Contains(assignment.LecturerId));
    }
}
