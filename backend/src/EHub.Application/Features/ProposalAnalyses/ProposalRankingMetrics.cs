namespace EHub.Application.Features.ProposalAnalyses;

public sealed record ProposalRankingMetricSet(
    double PrecisionAtK,
    double RecallAtK,
    double NdcgAtK);

public static class ProposalRankingMetrics
{
    public static ProposalRankingMetricSet Calculate(
        IReadOnlyList<string> rankedProposalIds,
        IReadOnlyDictionary<string, int> relevanceGrades,
        int k)
    {
        ArgumentNullException.ThrowIfNull(rankedProposalIds);
        ArgumentNullException.ThrowIfNull(relevanceGrades);
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k));
        if (rankedProposalIds.Any(string.IsNullOrWhiteSpace)
            || rankedProposalIds.Distinct(StringComparer.Ordinal).Count() != rankedProposalIds.Count)
            throw new ArgumentException("Ranked proposal identifiers must be non-empty and unique.", nameof(rankedProposalIds));
        if (relevanceGrades.Count == 0
            || relevanceGrades.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0))
            throw new ArgumentException("At least one positive relevance grade is required.", nameof(relevanceGrades));

        var topK = rankedProposalIds.Take(k).ToArray();
        var relevantRetrieved = topK.Count(id => relevanceGrades.ContainsKey(id));
        var precision = relevantRetrieved / (double)k;
        var recall = relevantRetrieved / (double)relevanceGrades.Count;
        var dcg = DiscountedCumulativeGain(topK.Select(id => relevanceGrades.GetValueOrDefault(id)));
        var idealDcg = DiscountedCumulativeGain(relevanceGrades.Values.OrderByDescending(value => value).Take(k));

        return new ProposalRankingMetricSet(
            precision,
            recall,
            idealDcg == 0 ? 0 : dcg / idealDcg);
    }

    private static double DiscountedCumulativeGain(IEnumerable<int> grades) =>
        grades.Select((grade, index) => (Math.Pow(2, grade) - 1) / Math.Log2(index + 2d)).Sum();
}
