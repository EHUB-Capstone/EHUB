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
