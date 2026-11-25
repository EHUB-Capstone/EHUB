using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase;

/// <summary>
/// Ownership rule for showcase pages: a showcase belongs to a project lineage and is managed by the active
/// members of the team that owns the newest project of that lineage.
/// </summary>
internal static class ShowcaseAccess
{
    public static bool IsStudent(string role) => string.Equals(role, SystemRoles.Student, StringComparison.OrdinalIgnoreCase);

    /// <summary>Newest project of every lineage whose team the user is an active member of.</summary>
    public static IQueryable<Project> OwnedProjects(IApplicationDbContext context, Guid userId) =>
        context.Projects.AsNoTracking().Where(project =>
            project.Team.TeamMembers.Any(member =>
                member.CountsTowardActiveTeam &&
                member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active &&
                member.ClassStudent.Student.UserId == userId) &&
            !context.Projects.Any(other =>
                other.ProjectLineageId == project.ProjectLineageId && other.CreatedAt > project.CreatedAt));

    public static bool IsAdmin(string role) => string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);

    public static bool IsLecturer(string role) => string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

    public static bool IsModerator(string role) => IsAdmin(role) || IsLecturer(role);

    /// <summary>
    /// Showcases the caller may review: every showcase for an admin, and only those whose newest project belongs to a
    /// class the lecturer is assigned to. Any other role gets nothing.
    /// </summary>
    public static IQueryable<ShowcaseProject> ModeratableShowcases(IApplicationDbContext context, Guid userId, string role)
    {
        if (IsAdmin(role)) return context.ShowcaseProjects;
        if (!IsLecturer(role)) return context.ShowcaseProjects.Where(_ => false);

        return context.ShowcaseProjects.Where(showcase => context.Projects.Any(project =>
            project.ProjectLineageId == showcase.ProjectLineageId &&
            (project.Team.Class.PrimaryLecturerId == userId ||
             project.Team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId)) &&
            !context.Projects.Any(other =>
                other.ProjectLineageId == project.ProjectLineageId && other.CreatedAt > project.CreatedAt)));
    }

    /// <summary>
    /// Reports a staff member may review. A showcase-page report belongs to the class that owns the page. For a
    /// comment report, the lecturer of the comment author's active class may also review it: commenters can be
    /// students from a different class than the team that owns the page.
    /// </summary>
    public static IQueryable<ShowcaseReport> ModeratableReports(IApplicationDbContext context, Guid userId, string role)
    {
        if (IsAdmin(role)) return context.ShowcaseReports;
        if (!IsLecturer(role)) return context.ShowcaseReports.Where(_ => false);

        var showcases = ModeratableShowcases(context, userId, role);
        return context.ShowcaseReports.Where(report =>
            showcases.Any(showcase => showcase.Id == report.ShowcaseProjectId) ||
            (report.CommentId.HasValue && context.ShowcaseComments.IgnoreQueryFilters().Any(comment =>
                comment.Id == report.CommentId && context.ClassStudents.Any(enrollment =>
                    enrollment.Student.UserId == comment.AuthorUserId
                    && enrollment.EnrollmentStatus == EnrollmentStatus.Active
                    && (enrollment.Class.PrimaryLecturerId == userId
                        || enrollment.Class.ClassLecturers.Any(assignment => assignment.LecturerId == userId))))));
    }

    /// <summary>
    /// Gets the lecturer(s) responsible for a student's active class. The fallback maintains a review path for
    /// users without a student enrollment, such as mentors and alumni.
    /// </summary>
    public static async Task<IReadOnlyCollection<Guid>> ResponsibleLecturerUserIdsForStudentAsync(
        IApplicationDbContext context, Guid studentUserId, IReadOnlyCollection<Guid> fallback, CancellationToken cancellationToken)
    {
        var classIds = context.ClassStudents.AsNoTracking()
            .Where(enrollment => enrollment.Student.UserId == studentUserId
                && enrollment.EnrollmentStatus == EnrollmentStatus.Active)
            .Select(enrollment => enrollment.ClassId);
        var primaryLecturerIds = context.Classes.AsNoTracking()
            .Where(@class => classIds.Contains(@class.Id) && @class.PrimaryLecturerId.HasValue)
            .Select(@class => @class.PrimaryLecturerId!.Value);
        var assignedLecturerIds = context.ClassLecturers.AsNoTracking()
            .Where(assignment => classIds.Contains(assignment.ClassId))
            .Select(assignment => assignment.LecturerId);
        var lecturerIds = await primaryLecturerIds.Concat(assignedLecturerIds).Distinct().ToArrayAsync(cancellationToken);
        return lecturerIds.Length == 0 ? fallback : lecturerIds;
    }

    /// <summary>Newest project of a lineage with the team, class and people around it.</summary>
    public static async Task<LatestProject?> LatestProjectAsync(
        IApplicationDbContext context, Guid lineageId, CancellationToken cancellationToken)
    {
        var row = await context.Projects.AsNoTracking()
            .Where(project => project.ProjectLineageId == lineageId)
            .OrderByDescending(project => project.CreatedAt)
            .Select(project => new
            {
                project.Id,
                project.TeamId,
                project.Name,
                project.Description,
                project.Problem,
                project.Solution,
                project.TargetUsers,
                project.Team.TeamName,
                project.Team.Class.ClassCode,
                project.Team.Class.PrimaryLecturerId,
                AssignedLecturerIds = project.Team.Class.ClassLecturers.Select(assignment => assignment.LecturerId).ToList(),
                Members = project.Team.TeamMembers
                    .Where(member => member.CountsTowardActiveTeam &&
                                     member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active)
                    .Select(member => new
                    {
                        member.ClassStudent.Student.UserId,
                        member.ClassStudent.Student.FullName,
                        member.ClassStudent.Student.AvatarUrl,
                        member.RoleInTeam
                    }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        var lecturerIds = row.AssignedLecturerIds.ToList();
        if (row.PrimaryLecturerId is { } primary) lecturerIds.Add(primary);
        return new LatestProject(
            row.Id,
            row.TeamId,
            row.Name,
            row.Description ?? string.Empty,
            row.Problem,
            row.Solution,
            row.TargetUsers,
            row.TeamName,
            row.ClassCode,
            lecturerIds.Distinct().ToArray(),
            row.Members.Select(member => new LatestProjectMember(
                member.UserId, member.FullName, member.AvatarUrl, member.RoleInTeam.ToString())).ToArray());
    }

    public static Task<bool> OwnsLineageAsync(
        IApplicationDbContext context, Guid lineageId, Guid userId, CancellationToken cancellationToken) =>
        OwnedProjects(context, userId).AnyAsync(project => project.ProjectLineageId == lineageId, cancellationToken);
}
