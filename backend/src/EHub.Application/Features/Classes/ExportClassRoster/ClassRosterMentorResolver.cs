using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.ExportClassRoster;

internal static class ClassRosterMentorResolver
{
    internal static async Task<IReadOnlyDictionary<Guid, ClassRosterMentorNames>> LoadByTeamAsync(
        IApplicationDbContext context,
        IEnumerable<Guid> classIds,
        CancellationToken cancellationToken)
    {
        var targetClassIds = classIds.Distinct().ToArray();
        if (targetClassIds.Length == 0)
        {
            return new Dictionary<Guid, ClassRosterMentorNames>();
        }

        var assignments = await context.MentorAssignments
            .AsNoTracking()
            .Where(assignment =>
                targetClassIds.Contains(assignment.Team.ClassId) &&
                assignment.Team.Status == TeamStatus.Active &&
                assignment.Status == MentorAssignmentStatus.Active &&
                assignment.EndedAt == null)
            .OrderByDescending(assignment => assignment.AssignedAt)
            .ThenBy(assignment => assignment.Id)
            .Select(assignment => new
            {
                assignment.TeamId,
                assignment.Slot,
                assignment.MentorProfile.User.FullName
            })
            .ToListAsync(cancellationToken);

        return assignments
            .GroupBy(assignment => assignment.TeamId)
            .ToDictionary(
                group => group.Key,
                group => new ClassRosterMentorNames(
                    group.FirstOrDefault(assignment => assignment.Slot == MentorType.Enterprise)?.FullName
                        ?? string.Empty,
                    group.FirstOrDefault(assignment => assignment.Slot == MentorType.Academic)?.FullName
                        ?? string.Empty));
    }
}
