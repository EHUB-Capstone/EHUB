using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors;

internal sealed record AllocationTeam(Guid Id, Guid ClassId, string Code, string Name);

internal sealed record AllocationMentor(Guid Id, string Name, string Email, MentorType Type, bool IsTemporary = false);

internal sealed record AllocationExistingAssignment(Guid TeamId, Guid MentorProfileId, MentorType Slot);

internal sealed record AllocationProposal(AllocationTeam Team, AllocationMentor Mentor, int ResultingLoad,
    MentorFitRanking? Ranking = null);

internal sealed record AllocationOutcome(
    IReadOnlyList<AllocationProposal> Proposals,
    IReadOnlyList<string> Warnings);

internal enum AllocationStrategy
{
    Balanced,
    Random,
    Ai
}

/// <summary>
/// Chooses one mentor from a pool that already satisfies every hard rule. New strategies (for example an
/// AI-assisted one) plug in here without touching the rules, the preview or the commit.
/// </summary>
internal interface IMentorSelector
{
    AllocationMentor? Pick(AllocationTeam team, AllocationMentor[] pool, IReadOnlyDictionary<Guid, int> loads, Random random);
}

/// <summary>Prefers the mentors with the fewest teams; ties are broken at random.</summary>
internal sealed class BalancedMentorSelector : IMentorSelector
{
    public AllocationMentor Pick(AllocationTeam team, AllocationMentor[] pool, IReadOnlyDictionary<Guid, int> loads, Random random)
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
    public AllocationMentor Pick(AllocationTeam team, AllocationMentor[] pool, IReadOnlyDictionary<Guid, int> loads, Random random) =>
        pool[random.Next(pool.Length)];
}

/// <summary>Prefers relevant evidence; otherwise proposes the least-loaded eligible profile for review.</summary>
internal sealed class AiMentorSelector(
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>> rankings) : IMentorSelector
{
    public AllocationMentor? Pick(AllocationTeam team, AllocationMentor[] pool,
        IReadOnlyDictionary<Guid, int> loads, Random random)
    {
        if (!rankings.TryGetValue(team.Id, out var teamRankings)) return null;
        var persistent = pool.Where(mentor => !mentor.IsTemporary).ToArray();
        var evidenced = persistent.Where(mentor => teamRankings.TryGetValue(mentor.Id, out var fit)
                && fit.HasEvidence && IsValid(fit))
            .OrderByDescending(mentor => teamRankings[mentor.Id].RankingValue ?? teamRankings[mentor.Id].Score)
            .ThenBy(mentor => loads[mentor.Id])
            .ThenBy(_ => random.Next())
            .FirstOrDefault();
        if (evidenced is not null) return evidenced;

        // A model response with missing or invalid entries is not a no-match result.
        // Only a complete successful response may produce review-needed proposals.
        if (persistent.Length == 0 || persistent.Any(mentor =>
                !teamRankings.TryGetValue(mentor.Id, out var fit) || !IsValid(fit))) return null;
        return persistent.OrderBy(mentor => loads[mentor.Id])
            .ThenBy(_ => random.Next())
            .First();
    }

    private static bool IsValid(MentorFitRanking fit) => fit.Score is >= 0 and <= 100 &&
        fit.Reasons is not null && double.IsFinite(fit.RankingValue ?? fit.Score);
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
        AllocationStrategy strategy = AllocationStrategy.Balanced,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>? rankings = null)
    {
        var selector = CreateSelector(strategy, rankings);
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
                var mentor = selector.Pick(team, pool, loads, random);
                if (mentor is null)
                {
                    warnings.Add($"Nhóm {team.Code}: chưa có kết quả AI hợp lệ để đề xuất mentor {slot}. Cần kiểm tra mô tả dự án hoặc hồ sơ mentor.");
                    continue;
                }
                loads[mentor.Id]++;
                var ranking = strategy == AllocationStrategy.Ai ? rankings![team.Id][mentor.Id] : null;
                if (ranking is { HasEvidence: false })
                {
                    ranking = ranking with
                    {
                        Score = 0, RankingValue = 0,
                        Reasons = ranking.Reasons.Append("Đề xuất theo tải hiện tại vì chưa có bằng chứng chuyên môn phù hợp; cần admin rà soát.")
                            .Distinct(StringComparer.Ordinal).ToArray()
                    };
                    warnings.Add($"Nhóm {team.Code}: đã đề xuất mentor {slot} theo tải hiện tại do chưa có bằng chứng chuyên môn phù hợp; admin cần rà soát trước khi chấp nhận.");
                }
                proposals.Add(new AllocationProposal(team, mentor, loads[mentor.Id], ranking));
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

    private static IMentorSelector CreateSelector(AllocationStrategy strategy,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>? rankings) => strategy switch
    {
        AllocationStrategy.Balanced => new BalancedMentorSelector(),
        AllocationStrategy.Random => new RandomMentorSelector(),
        AllocationStrategy.Ai => new AiMentorSelector(rankings ?? new Dictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>()),
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown allocation strategy.")
    };
}
