using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Admin.Mentors;

/// <summary>
/// Loads the read-only data <see cref="MentorRetentionPlanner"/> needs. All queries are no-tracking.
/// </summary>
internal static class MentorRetentionDataLoader
{
    public static async Task<RetentionInput> LoadAsync(
        IApplicationDbContext context,
        Guid semesterId,
        IReadOnlyCollection<Guid> targetTeamIds,
        IReadOnlyCollection<AllocationExistingAssignment> existingAssignments,
        CancellationToken cancellationToken)
    {
        var targetTeams = await context.Teams.AsNoTracking()
            .Where(team => targetTeamIds.Contains(team.Id))
            .Select(team => new RetentionTargetTeam(
                team.Id, team.TeamCode, team.TeamName, team.Class.ClassCode, team.Class.Course.Code, team.PreviousTeamId))
            .ToListAsync(cancellationToken);

        var continuedSourceTeamIds = await context.Teams.AsNoTracking()
            .Where(team => team.Class.SemesterId == semesterId && team.Status == TeamStatus.Active && team.PreviousTeamId != null)
            .Select(team => team.PreviousTeamId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var previousSemesterId = await FindPreviousSemesterIdAsync(context, semesterId, cancellationToken);
        var linkedSourceIds = targetTeams.Where(team => team.PreviousTeamId != null).Select(team => team.PreviousTeamId!.Value).Distinct().ToArray();

        var sourceTeams = await context.Teams.AsNoTracking()
            .Where(team => linkedSourceIds.Contains(team.Id)
                || (previousSemesterId != null
                    && team.Class.SemesterId == previousSemesterId
                    && team.Class.Course.Code == MentorRetentionPlanner.SourceCourseCode
                    && team.Status != TeamStatus.Disabled))
            .Select(team => new RetentionSourceTeam(
                team.Id, team.TeamCode, team.TeamName, team.Class.ClassCode, team.Class.Course.Code,
                team.Class.SemesterId, team.Class.CompletedAtUtc))
            .ToListAsync(cancellationToken);
        var sourceIds = sourceTeams.Select(team => team.Id).ToArray();

        var sourceAssignments = await context.MentorAssignments.AsNoTracking()
            .Where(item => sourceIds.Contains(item.TeamId)
                && (item.Status == MentorAssignmentStatus.Active || item.Status == MentorAssignmentStatus.Ended))
            .Select(item => new RetentionSourceAssignment(
                item.TeamId, item.Slot, item.MentorProfileId, item.AssignedAt, item.Status, item.EndedAt))
            .ToListAsync(cancellationToken);

        var mentorIds = sourceAssignments.Select(item => item.MentorProfileId).Distinct().ToArray();
        var mentors = await context.MentorProfiles.AsNoTracking()
            .Where(profile => mentorIds.Contains(profile.Id))
            .Select(profile => new RetentionMentor(
                profile.Id,
                profile.User.FullName,
                profile.User.Email,
                profile.Type,
                profile.Status == MentorProfileStatus.Active && profile.User.Status == UserStatus.Active,
                context.SemesterStaffAssignments.Any(staff =>
                    staff.SemesterId == semesterId
                    && staff.UserId == profile.UserId
                    && staff.Role == SemesterStaffRole.Mentor
                    && staff.Status == SemesterStaffStatus.Active)))
            .ToListAsync(cancellationToken);

        return new RetentionInput(
            semesterId, targetTeams, continuedSourceTeamIds, sourceTeams, sourceAssignments, mentors, existingAssignments);
    }

    private static async Task<Guid?> FindPreviousSemesterIdAsync(
        IApplicationDbContext context, Guid semesterId, CancellationToken cancellationToken)
    {
        var semesters = (await context.Semesters.AsNoTracking()
                .Select(semester => new { semester.Id, semester.StartDate, semester.Year, semester.Term })
                .ToListAsync(cancellationToken))
            .Select(semester => new
            {
                semester.Id,
                SortDate = semester.StartDate ?? new DateOnly(
                    Math.Clamp(semester.Year, 1, 9998),
                    semester.Term switch { SemesterTerm.Spring => 1, SemesterTerm.Summer => 5, _ => 9 },
                    1)
            })
            .ToList();

        var target = semesters.FirstOrDefault(semester => semester.Id == semesterId);
        if (target is null) return null;

        return semesters
            .Where(semester => semester.Id != target.Id && semester.SortDate < target.SortDate)
            .OrderByDescending(semester => semester.SortDate)
            .Select(semester => (Guid?)semester.Id)
            .FirstOrDefault();
    }
}
