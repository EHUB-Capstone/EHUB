using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Admin.Users.ManageUsers;

// Why an account cannot be deleted. An account is deleted only when nothing in the system still depends on it, so
// pages that list classes, teams, assignments or evaluations never meet a person who has disappeared.
internal static class UserDeletionRules
{
    private const int ExampleLimit = 3;

    public static async Task<List<string>> FindBlockersAsync(IApplicationDbContext context, User user, bool isLastAdmin, CancellationToken token)
    {
        var reasons = new List<string>();
        if (isLastAdmin) reasons.Add("it is the last administrator account");

        if (user.Student is { } student)
        {
            var classes = await context.ClassStudents.CountAsync(item => item.StudentId == student.Id, token);
            if (classes > 0) reasons.Add($"the student is enrolled in {Plural(classes, "class", "classes")}");
            var teams = await context.TeamMembers.CountAsync(item => item.StudentId == student.Id, token);
            if (teams > 0) reasons.Add($"the student belongs to {Plural(teams, "team")}");
        }

        var classCodes = await context.Classes.AsNoTracking()
            .Where(item => item.PrimaryLecturerId == user.Id || context.ClassLecturers.Any(link => link.ClassId == item.Id && link.LecturerId == user.Id))
            .OrderBy(item => item.ClassCode).Select(item => item.ClassCode).ToListAsync(token);
        if (classCodes.Count > 0)
            reasons.Add($"the lecturer teaches {Plural(classCodes.Count, "class", "classes")} ({Examples(classCodes)})");

        var profile = await context.MentorProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == user.Id, token);
        if (profile is not null)
        {
            var current = await context.MentorAssignments.CountAsync(item => item.MentorProfileId == profile.Id &&
                item.Status == Domain.Enums.MentorAssignmentStatus.Active && item.EndedAt == null, token);
            var past = await context.MentorAssignments.CountAsync(item => item.MentorProfileId == profile.Id, token) - current;
            if (current > 0) reasons.Add($"the mentor is assigned to {Plural(current, "team")} right now");
            if (past > 0) reasons.Add($"the mentor has {Plural(past, "past team assignment")} in the history");
        }

        var evaluations = await context.Evaluations.CountAsync(item => item.EvaluatorId == user.Id, token);
        if (evaluations > 0) reasons.Add($"it has given {Plural(evaluations, "evaluation")}");
        var createdClasses = await context.Classes.CountAsync(item => item.CreatedById == user.Id, token);
        if (createdClasses > 0) reasons.Add($"it created {Plural(createdClasses, "class", "classes")}");
        var createdProjects = await context.Projects.CountAsync(item => item.CreatedById == user.Id, token);
        if (createdProjects > 0) reasons.Add($"it created {Plural(createdProjects, "project")}");
        return reasons;
    }

    public static string Explain(string displayName, IReadOnlyCollection<string> reasons) =>
        $"{displayName} cannot be deleted because {string.Join("; ", reasons)}. " +
        "Remove or reassign them first, or block the account instead so it can no longer sign in.";

    private static string Plural(int count, string singular, string? plural = null) => $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
    private static string Examples(IReadOnlyList<string> values) =>
        string.Join(", ", values.Take(ExampleLimit)) + (values.Count > ExampleLimit ? ", ..." : string.Empty);
}
