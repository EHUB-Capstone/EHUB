using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors;

internal sealed record AllocationTeam(Guid Id, Guid ClassId, string Code, string Name);

internal sealed record AllocationMentor(Guid Id, string Name, string Email, MentorType Type);

internal sealed record AllocationExistingAssignment(Guid TeamId, Guid MentorProfileId, MentorType Slot);

internal sealed record AllocationProposal(AllocationTeam Team, AllocationMentor Mentor, int ResultingLoad);

internal sealed record AllocationOutcome(
    IReadOnlyList<AllocationProposal> Proposals,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Pure allocation logic: fills only the empty mentor slots of the given teams.
/// It never reads or writes the database so it can be unit tested in isolation.
/// Hard rules (matching mentor type, only empty slots, never replacing an existing mentor)
/// are applied before the selection step, which currently balances the mentor load.
/// </summary>
internal static class MentorAllocationEngine
{
    private static readonly MentorType[] SlotOrder = [MentorType.Enterprise, MentorType.Academic];

    public static AllocationOutcome Allocate(
        IReadOnlyCollection<AllocationTeam> teams,
        IReadOnlyCollection<AllocationMentor> mentors,
        IReadOnlyCollection<AllocationExistingAssignment> existingAssignments,
        int seed)
    {
        var random = new Random(seed);
        var proposals = new List<AllocationProposal>();
        var warnings = new List<string>();

        foreach (var slot in SlotOrder)
        {
            var pool = GetEligiblePool(mentors, slot);
            var missing = GetTeamsMissingSlot(teams, existingAssignments, slot)
                .OrderBy(_ => random.Next())
                .ToArray();

            if (missing.Length > 0 && pool.Length == 0)
            {
                warnings.Add($"No active {slot} mentors are available for {missing.Length} missing team slots.");
                continue;
            }

            var loads = pool.ToDictionary(
                mentor => mentor.Id,
                mentor => existingAssignments.Count(assignment => assignment.MentorProfileId == mentor.Id));

            foreach (var team in missing)
            {
                var mentor = PickBalanced(pool, loads, random);
                loads[mentor.Id]++;
                proposals.Add(new AllocationProposal(team, mentor, loads[mentor.Id]));
            }
        }

        return new AllocationOutcome(proposals, warnings);
    }

    private static AllocationMentor[] GetEligiblePool(IReadOnlyCollection<AllocationMentor> mentors, MentorType slot) =>
        mentors.Where(mentor => mentor.Type == slot).ToArray();

    private static IEnumerable<AllocationTeam> GetTeamsMissingSlot(
        IReadOnlyCollection<AllocationTeam> teams,
        IReadOnlyCollection<AllocationExistingAssignment> existingAssignments,
        MentorType slot) =>
        teams.Where(team => existingAssignments.All(assignment => assignment.TeamId != team.Id || assignment.Slot != slot));

    private static AllocationMentor PickBalanced(
        AllocationMentor[] pool,
        IReadOnlyDictionary<Guid, int> loads,
        Random random)
    {
        var minimum = loads.Values.Min();
        var candidates = pool
            .Where(mentor => loads[mentor.Id] == minimum)
            .OrderBy(_ => random.Next())
            .ToArray();
        return candidates[0];
    }
}
