using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors;

internal sealed record AllocationTeam(Guid Id, Guid ClassId, string Code, string Name);

internal sealed record AllocationMentor(Guid Id, string Name, string Email, MentorType Type);

internal sealed record AllocationExistingAssignment(Guid TeamId, Guid MentorProfileId, MentorType Slot);

internal sealed record AllocationProposal(AllocationTeam Team, AllocationMentor Mentor, int ResultingLoad);

internal sealed record AllocationOutcome(
    IReadOnlyList<AllocationProposal> Proposals,
    IReadOnlyList<string> Warnings);

internal enum AllocationStrategy
{
    Balanced,
    Random
}

/// <summary>
/// Chooses one mentor from a pool that already satisfies every hard rule. New strategies (for example an
/// AI-assisted one) plug in here without touching the rules, the preview or the commit.
/// </summary>
internal interface IMentorSelector
{
    AllocationMentor Pick(AllocationMentor[] pool, IReadOnlyDictionary<Guid, int> loads, Random random);
}

/// <summary>Prefers the mentors with the fewest teams; ties are broken at random.</summary>
internal sealed class BalancedMentorSelector : IMentorSelector
{
    public AllocationMentor Pick(AllocationMentor[] pool, IReadOnlyDictionary<Guid, int> loads, Random random)
    {
        var minimum = loads.Values.Min();
        var candidates = pool
            .Where(mentor => loads[mentor.Id] == minimum)
            .OrderBy(_ => random.Next())
            .ToArray();
        return candidates[0];
    }
}

/// <summary>Picks any eligible mentor with equal probability, ignoring the current load.</summary>
internal sealed class RandomMentorSelector : IMentorSelector
{
    public AllocationMentor Pick(AllocationMentor[] pool, IReadOnlyDictionary<Guid, int> loads, Random random) =>
        pool[random.Next(pool.Length)];
}

/// <summary>
/// Pure allocation logic: fills only the empty mentor slots of the given teams.
/// It never reads or writes the database so it can be unit tested in isolation.
/// Hard rules (matching mentor type, only empty slots, never replacing an existing mentor)
/// are applied before the selection step, which is delegated to an <see cref="IMentorSelector"/>.
/// </summary>
internal static class MentorAllocationEngine
{
    private static readonly MentorType[] SlotOrder = [MentorType.Enterprise, MentorType.Academic];

    public static AllocationOutcome Allocate(
        IReadOnlyCollection<AllocationTeam> teams,
        IReadOnlyCollection<AllocationMentor> mentors,
        IReadOnlyCollection<AllocationExistingAssignment> existingAssignments,
        int seed,
        AllocationStrategy strategy = AllocationStrategy.Balanced)
    {
        var selector = CreateSelector(strategy);
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
                var mentor = selector.Pick(pool, loads, random);
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

    private static IMentorSelector CreateSelector(AllocationStrategy strategy) => strategy switch
    {
        AllocationStrategy.Balanced => new BalancedMentorSelector(),
        AllocationStrategy.Random => new RandomMentorSelector(),
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown allocation strategy.")
    };
}
