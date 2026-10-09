using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors;

internal enum ManualConflictKind
{
    SlotOccupied,
    EditRejected
}

internal sealed record ManualEdit(Guid TeamId, MentorType Slot, Guid? MentorProfileId, bool Replace, string? Reason);

internal sealed record ExistingSlot(Guid AssignmentId, Guid TeamId, MentorType Slot, Guid MentorProfileId, string MentorName);

internal sealed record ManualAssignment(
    AllocationTeam Team, AllocationMentor Mentor, MentorType Slot, ExistingSlot? Replaces, string? Reason);

internal sealed record ManualExclusion(Guid TeamId, MentorType Slot);

internal sealed record ManualConflict(
    ManualConflictKind Kind, AllocationTeam? Team, MentorType Slot, ExistingSlot? Current, AllocationMentor? Proposed, string Message);

internal sealed record ManualPlan(
    IReadOnlyList<ManualAssignment> Assignments,
    IReadOnlyList<ManualExclusion> Exclusions,
    IReadOnlyList<ManualConflict> Conflicts);

/// <summary>
/// Validates the admin's hand edits of an allocation preview. An edit never overwrites a mentor silently:
/// putting a different mentor into an occupied slot is reported as a conflict unless the admin explicitly asks
/// to replace the current mentor and gives a reason.
/// </summary>
internal static class MentorManualEditPlanner
{
    public const int MinimumReasonLength = 3;
    public const int MaximumReasonLength = 1_000;

    public static ManualPlan Plan(
        IReadOnlyCollection<ManualEdit> edits,
        IReadOnlyCollection<AllocationTeam> teamsInScope,
        IReadOnlyCollection<AllocationMentor> eligibleMentors,
        IReadOnlyCollection<ExistingSlot> existingSlots)
    {
        var teams = teamsInScope.ToDictionary(team => team.Id);
        var mentors = eligibleMentors.ToDictionary(mentor => mentor.Id);
        var occupants = existingSlots.ToDictionary(slot => (slot.TeamId, slot.Slot));
        var seen = new HashSet<(Guid, MentorType)>();

        var assignments = new List<ManualAssignment>();
        var exclusions = new List<ManualExclusion>();
        var conflicts = new List<ManualConflict>();

        foreach (var edit in edits)
        {
            teams.TryGetValue(edit.TeamId, out var team);
            occupants.TryGetValue((edit.TeamId, edit.Slot), out var current);
            var proposed = edit.MentorProfileId is { } id && mentors.TryGetValue(id, out var found) ? found : null;

            ManualConflict Reject(string message) => new(ManualConflictKind.EditRejected, team, edit.Slot, current, proposed, message);

            if (team is null)
            {
                conflicts.Add(Reject("The team is not part of the selected scope or is not active."));
                continue;
            }

            if (!seen.Add((edit.TeamId, edit.Slot)))
            {
                conflicts.Add(Reject("Only one edit per team slot is allowed."));
                continue;
            }

            if (edit.MentorProfileId is null)
            {
                if (current is not null) conflicts.Add(Reject("An existing mentor cannot be removed here. End the assignment instead."));
                else exclusions.Add(new ManualExclusion(team.Id, edit.Slot));
                continue;
            }

            if (proposed is null)
            {
                conflicts.Add(Reject("The selected mentor is not active in this semester."));
                continue;
            }

            if (proposed.Type != edit.Slot)
            {
                conflicts.Add(Reject($"A {proposed.Type} mentor cannot fill the {edit.Slot} slot."));
                continue;
            }

            if (current is null)
            {
                assignments.Add(new ManualAssignment(team, proposed, edit.Slot, null, null));
                continue;
            }

            if (current.MentorProfileId == proposed.Id) continue;

            if (!edit.Replace)
            {
                conflicts.Add(new ManualConflict(
                    ManualConflictKind.SlotOccupied, team, edit.Slot, current, proposed,
                    $"This slot already has {current.MentorName}. The current mentor is kept unless you choose to replace them."));
                continue;
            }

            var reason = edit.Reason?.Trim();
            if (reason is null || reason.Length < MinimumReasonLength || reason.Length > MaximumReasonLength)
            {
                conflicts.Add(Reject($"A reason between {MinimumReasonLength} and {MaximumReasonLength} characters is required to replace a mentor."));
                continue;
            }

            assignments.Add(new ManualAssignment(team, proposed, edit.Slot, current, reason));
        }

        return new ManualPlan(assignments, exclusions, conflicts);
    }
}
