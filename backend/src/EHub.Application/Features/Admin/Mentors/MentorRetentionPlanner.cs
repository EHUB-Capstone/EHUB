using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors;

internal enum MentorRetentionSkipReason
{
    MentorNotActiveInSemester,
    MentorUnavailable,
    SlotAlreadyFilled,
    NoContinuedTeam
}

internal sealed record RetentionTargetTeam(
    Guid Id, string Code, string Name, string ClassCode, string CourseCode, Guid? PreviousTeamId);

internal sealed record RetentionSourceTeam(
    Guid Id, string Code, string Name, string ClassCode, string CourseCode, Guid SemesterId, DateTime? ClassCompletedAtUtc);

internal sealed record RetentionSourceAssignment(
    Guid TeamId, MentorType Slot, Guid MentorProfileId, DateTime AssignedAt, MentorAssignmentStatus Status, DateTime? EndedAt);

internal sealed record RetentionMentor(
    Guid Id, string Name, string Email, MentorType Type, bool IsAccountActive, bool IsActiveInSemester);

internal sealed record RetentionInput(
    Guid TargetSemesterId,
    IReadOnlyCollection<RetentionTargetTeam> TargetTeams,
    IReadOnlyCollection<Guid> ContinuedSourceTeamIds,
    IReadOnlyCollection<RetentionSourceTeam> SourceTeams,
    IReadOnlyCollection<RetentionSourceAssignment> SourceAssignments,
    IReadOnlyCollection<RetentionMentor> Mentors,
    IReadOnlyCollection<AllocationExistingAssignment> ExistingAssignments);

internal sealed record RetentionProposal(
    RetentionTargetTeam Team, RetentionMentor Mentor, MentorType Slot, string SourceTeamCode);

internal sealed record RetentionSkip(
    MentorRetentionSkipReason Reason,
    RetentionTargetTeam? Team,
    string SourceTeamCode,
    MentorType Slot,
    Guid MentorProfileId,
    string MentorName,
    string MentorEmail);

internal sealed record RetentionPlan(
    IReadOnlyList<RetentionProposal> Retained,
    IReadOnlyList<RetentionSkip> Skipped);

/// <summary>
/// Pure rules that decide which previous mentors are proposed to stay with a team that continues into the
/// current semester. A team continues only along the EXE101 to EXE201 path, and only through the team
/// lineage link (<see cref="RetentionTargetTeam.PreviousTeamId"/>), never through the team name.
/// </summary>
internal static class MentorRetentionPlanner
{
    public const string SourceCourseCode = "EXE101";
    public const string TargetCourseCode = "EXE201";

    private static readonly MentorType[] SlotOrder = [MentorType.Enterprise, MentorType.Academic];

    public static RetentionPlan Plan(RetentionInput input)
    {
        var sourcesById = input.SourceTeams.ToDictionary(team => team.Id);
        var mentorsById = input.Mentors.ToDictionary(mentor => mentor.Id);
        var finalAssignments = ResolveFinalAssignments(input.SourceAssignments, sourcesById);
        var existingBySlot = input.ExistingAssignments.ToLookup(item => (item.TeamId, item.Slot));

        var retained = new List<RetentionProposal>();
        var skipped = new List<RetentionSkip>();

        foreach (var team in input.TargetTeams.OrderBy(item => item.ClassCode, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal))
        {
            if (team.PreviousTeamId is not { } previousTeamId || !sourcesById.TryGetValue(previousTeamId, out var source)) continue;
            if (!IsContinuation(source, team, input.TargetSemesterId)) continue;

            foreach (var slot in SlotOrder)
            {
                if (!finalAssignments.TryGetValue((source.Id, slot), out var previous)) continue;
                mentorsById.TryGetValue(previous.MentorProfileId, out var mentor);

                var occupant = existingBySlot[(team.Id, slot)].FirstOrDefault();
                if (occupant is not null)
                {
                    if (occupant.MentorProfileId != previous.MentorProfileId)
                        skipped.Add(Skip(MentorRetentionSkipReason.SlotAlreadyFilled, team, source, slot, previous, mentor));
                    continue;
                }

                if (mentor is null || mentor.Type != slot || !mentor.IsAccountActive)
                    skipped.Add(Skip(MentorRetentionSkipReason.MentorUnavailable, team, source, slot, previous, mentor));
                else if (!mentor.IsActiveInSemester)
                    skipped.Add(Skip(MentorRetentionSkipReason.MentorNotActiveInSemester, team, source, slot, previous, mentor));
                else
                    retained.Add(new RetentionProposal(team, mentor, slot, source.Code));
            }
        }

        var continued = input.ContinuedSourceTeamIds.ToHashSet();
        foreach (var source in input.SourceTeams
                     .Where(item => IsSourceCourse(item.CourseCode) && item.SemesterId != input.TargetSemesterId && !continued.Contains(item.Id))
                     .OrderBy(item => item.ClassCode, StringComparer.Ordinal).ThenBy(item => item.Code, StringComparer.Ordinal))
        {
            foreach (var slot in SlotOrder)
            {
                if (!finalAssignments.TryGetValue((source.Id, slot), out var previous)) continue;
                mentorsById.TryGetValue(previous.MentorProfileId, out var mentor);
                skipped.Add(Skip(MentorRetentionSkipReason.NoContinuedTeam, null, source, slot, previous, mentor));
            }
        }

        return new RetentionPlan(retained, skipped);
    }

    /// <summary>
    /// The mentor a previous team ended the semester with, per slot: the latest assignment that is still active,
    /// or that ended together with the class being completed. An assignment ended earlier (for example replaced
    /// or removed by an admin) does not count as the team's mentor.
    /// </summary>
    private static Dictionary<(Guid TeamId, MentorType Slot), RetentionSourceAssignment> ResolveFinalAssignments(
        IReadOnlyCollection<RetentionSourceAssignment> assignments,
        IReadOnlyDictionary<Guid, RetentionSourceTeam> sourcesById)
    {
        var result = new Dictionary<(Guid, MentorType), RetentionSourceAssignment>();
        var candidates = assignments
            .Where(item => item.Status is MentorAssignmentStatus.Active or MentorAssignmentStatus.Ended)
            .GroupBy(item => (item.TeamId, item.Slot));

        foreach (var group in candidates)
        {
            var latest = group
                .OrderByDescending(item => item.AssignedAt)
                .ThenBy(item => item.Status == MentorAssignmentStatus.Active ? 0 : 1)
                .First();

            if (latest.Status == MentorAssignmentStatus.Active && latest.EndedAt is null)
            {
                result[group.Key] = latest;
                continue;
            }

            var completedAt = sourcesById.TryGetValue(group.Key.TeamId, out var team) ? team.ClassCompletedAtUtc : null;
            if (latest.EndedAt is { } endedAt && completedAt is { } completed && endedAt >= completed)
                result[group.Key] = latest;
        }

        return result;
    }

    private static bool IsContinuation(RetentionSourceTeam source, RetentionTargetTeam target, Guid targetSemesterId) =>
        IsSourceCourse(source.CourseCode)
        && string.Equals(target.CourseCode, TargetCourseCode, StringComparison.OrdinalIgnoreCase)
        && source.SemesterId != targetSemesterId;

    private static bool IsSourceCourse(string courseCode) =>
        string.Equals(courseCode, SourceCourseCode, StringComparison.OrdinalIgnoreCase);

    private static RetentionSkip Skip(
        MentorRetentionSkipReason reason,
        RetentionTargetTeam? team,
        RetentionSourceTeam source,
        MentorType slot,
        RetentionSourceAssignment previous,
        RetentionMentor? mentor) =>
        new(reason, team, source.Code, slot, previous.MentorProfileId, mentor?.Name ?? string.Empty, mentor?.Email ?? string.Empty);
}
