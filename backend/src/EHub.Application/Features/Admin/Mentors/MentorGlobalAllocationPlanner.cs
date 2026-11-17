using EHub.Application.Common.Interfaces.Services;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors;

/// <summary>Maximizes total fit after fixing the most even feasible semester loads.</summary>
internal static class MentorGlobalAllocationPlanner
{
    public static AllocationOutcome Allocate(IReadOnlyCollection<AllocationTeam> teams,
        IReadOnlyCollection<AllocationMentor> mentors,
        IReadOnlyCollection<AllocationExistingAssignment> existing, int seed,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>> rankings, CancellationToken ct,
        IReadOnlyCollection<MentorType>? slots = null)
    {
        var proposals = new List<AllocationProposal>();
        var warnings = new List<string>();
        var random = new Random(seed);
        foreach (var slot in new[] { MentorType.Enterprise, MentorType.Academic }.Where(type => slots is null || slots.Contains(type)))
        {
            ct.ThrowIfCancellationRequested();
            // Canonicalize before shuffling so the same inputs/seed give the same ties regardless of query ordering.
            var pool = Shuffle(mentors.Where(mentor => mentor.Type == slot && !mentor.IsTemporary)
                .OrderBy(mentor => mentor.Id), random);
            var missing = Shuffle(teams.Where(team => existing.All(item => item.TeamId != team.Id || item.Slot != slot))
                .OrderBy(team => team.Id), random);
            if (missing.Length == 0) continue;
            if (pool.Length == 0)
            {
                warnings.Add($"No active {slot} mentors are available for {missing.Length} missing team slots.");
                continue;
            }
            var valid = new List<AllocationTeam>();
            foreach (var team in missing)
            {
                if (!rankings.TryGetValue(team.Id, out var scores) || scores is null || pool.Any(mentor =>
                        !scores.TryGetValue(mentor.Id, out var fit) || !IsValid(fit)))
                {
                    warnings.Add($"Nhóm {team.Code}: chưa có kết quả AI hợp lệ để đề xuất mentor {slot}. Cần kiểm tra mô tả dự án hoặc hồ sơ mentor.");
                    continue;
                }
                valid.Add(team);
            }
            if (valid.Count == 0) continue;
            var loads = pool.ToDictionary(mentor => mentor.Id, mentor => existing.Count(item => item.MentorProfileId == mentor.Id));
            var assignment = Optimize(valid, pool, loads, rankings, ct);
            foreach (var team in valid)
            {
                var mentor = pool[assignment[team.Id]];
                loads[mentor.Id]++;
                var ranking = rankings[team.Id][mentor.Id];
                if (!ranking.HasEvidence)
                {
                    ranking = ranking with
                    {
                        Score = 0, RankingValue = 0,
                        Reasons = ranking.Reasons.Append("Đề xuất theo tải hiện tại vì chưa có bằng chứng chuyên môn phù hợp; cần admin rà soát.")
                            .Distinct(StringComparer.Ordinal).ToArray()
                    };
                    warnings.Add($"Nhóm {team.Code}: đã đề xuất mentor {slot} theo tải hiện tại do chưa có bằng chứng chuyên môn phù hợp; admin cần rà soát trước khi chấp nhận.");
                }
                ranking = ranking with { Reasons = ranking.Reasons
                    .Append("Tối ưu đồng thời các nhóm cần phân công, có tính tải cả học kỳ.").Distinct(StringComparer.Ordinal).ToArray() };
                proposals.Add(new AllocationProposal(team, mentor, loads[mentor.Id], ranking));
            }
        }
        return new AllocationOutcome(proposals, warnings);
    }

    private static bool IsValid(MentorFitRanking? fit) => fit is not null && fit.Score is >= 0 and <= 100 && fit.Reasons is not null &&
        double.IsFinite(fit.RankingValue ?? fit.Score) && (fit.RankingValue ?? fit.Score) is >= 0 and <= 100;

    private static T[] Shuffle<T>(IEnumerable<T> source, Random random)
    {
        var result = source.ToArray();
        random.Shuffle(result);
        return result;
    }

    private static Dictionary<Guid, int> Optimize(IReadOnlyList<AllocationTeam> teams, AllocationMentor[] mentors,
        IReadOnlyDictionary<Guid, int> loads,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>> rankings, CancellationToken ct)
    {
        var before = mentors.Select(mentor => loads[mentor.Id]).ToArray();
        // Water filling fixes lower quotas; remaining teams may add one to any mentor at the water level.
        // Existing overloads are immutable. This is the same most-even load distribution as filling the least
        // loaded mentor repeatedly, but leaves the choice of WHICH team to the global fit optimizer.
        var low = before.Min();
        var high = low + teams.Count;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            var required = before.Sum(value => (long)Math.Max(0, middle - value));
            if (required <= teams.Count) low = middle;
            else high = middle - 1;
        }
        var lower = before.Select(value => Math.Max(0, low - value)).ToArray();
        var extra = teams.Count - lower.Sum();
        var mentorStart = teams.Count + 1;
        var extras = mentorStart + mentors.Length;
        var sink = extras + 1;
        var flow = new MentorAssignmentFlow(sink + 1);
        var edges = new MentorAssignmentFlow.Edge[teams.Count, mentors.Length];
        for (var team = 0; team < teams.Count; team++)
        {
            ct.ThrowIfCancellationRequested();
            flow.Add(0, team + 1, 1, 0);
            for (var mentor = 0; mentor < mentors.Length; mentor++)
            {
                var fit = rankings[teams[team].Id][mentors[mentor].Id];
                var score = fit.HasEvidence ? fit.RankingValue ?? fit.Score : 0;
                edges[team, mentor] = flow.Add(team + 1, mentorStart + mentor, 1, 100 - score);
            }
        }
        for (var mentor = 0; mentor < mentors.Length; mentor++)
        {
            flow.Add(mentorStart + mentor, sink, lower[mentor], 0);
            if (before[mentor] <= low) flow.Add(mentorStart + mentor, extras, 1, 0);
        }
        // These sink capacities sum to exactly the team count, forcing every compulsory quota to be filled.
        flow.Add(extras, sink, extra, 0);
        flow.Send(0, sink, teams.Count, ct);
        return Enumerable.Range(0, teams.Count).ToDictionary(team => teams[team].Id, team =>
            Enumerable.Range(0, mentors.Length).Single(mentor => edges[team, mentor].Capacity == 0));
    }
}

/// <summary>Successive shortest paths with reduced costs; initial forward costs are nonnegative.</summary>
internal sealed class MentorAssignmentFlow(int nodes)
{
    internal sealed class Edge(int to, int reverse, int capacity, double cost)
    {
        public int To { get; } = to;
        public int Reverse { get; } = reverse;
        public int Capacity { get; set; } = capacity;
        public double Cost { get; } = cost;
    }

    private readonly List<Edge>[] graph = Enumerable.Range(0, nodes).Select(_ => new List<Edge>()).ToArray();

    public Edge Add(int from, int to, int capacity, double cost)
    {
        var forward = new Edge(to, graph[to].Count, capacity, cost);
        var reverse = new Edge(from, graph[from].Count, 0, -cost);
        graph[from].Add(forward);
        graph[to].Add(reverse);
        return forward;
    }

    public void Send(int source, int sink, int required, CancellationToken ct)
    {
        var potential = new double[nodes];
        for (var sent = 0; sent < required; sent++)
        {
            ct.ThrowIfCancellationRequested();
            var distance = Enumerable.Repeat(double.PositiveInfinity, nodes).ToArray();
            var previousNode = new int[nodes];
            var previousEdge = new int[nodes];
            var queue = new PriorityQueue<int, (double Distance, int Node)>();
            distance[source] = 0;
            queue.Enqueue(source, (0, source));
            while (queue.TryDequeue(out var node, out var priority))
            {
                if (priority.Distance != distance[node]) continue;
                for (var index = 0; index < graph[node].Count; index++)
                {
                    var edge = graph[node][index];
                    if (edge.Capacity == 0) continue;
                    // Roundoff in the potential arithmetic may produce tiny negative reduced costs.
                    var cost = Math.Max(0, edge.Cost + potential[node] - potential[edge.To]);
                    var next = distance[node] + cost;
                    if (next >= distance[edge.To]) continue;
                    distance[edge.To] = next;
                    previousNode[edge.To] = node;
                    previousEdge[edge.To] = index;
                    queue.Enqueue(edge.To, (next, edge.To));
                }
            }
            if (!double.IsFinite(distance[sink]))
                throw new InvalidOperationException("Balanced mentor allocation has no complete flow.");
            for (var node = 0; node < nodes; node++)
                if (double.IsFinite(distance[node])) potential[node] += distance[node];
            for (var node = sink; node != source; node = previousNode[node])
            {
                var edge = graph[previousNode[node]][previousEdge[node]];
                edge.Capacity--;
                graph[node][edge.Reverse].Capacity++;
            }
        }
    }
}
