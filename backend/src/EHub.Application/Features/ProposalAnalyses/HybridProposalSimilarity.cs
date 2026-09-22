namespace EHub.Application.Features.ProposalAnalyses;

public sealed record ProposalHybridSimilarityWeights(
    double Semantic,
    double TfIdf,
    double Jaccard);

public static class HybridProposalSimilarity
{
    public const string CurrentVersion = "proposal-hybrid-semantic-tfidf-jaccard-v1";
    public static readonly ProposalHybridSimilarityWeights CurrentWeights = new(
        Semantic: 0.70,
        TfIdf: 0.20,
        Jaccard: 0.10);

    public static double Calculate(double weightedSemanticSimilarity, double tfIdfSimilarity, double jaccardSimilarity)
    {
        Validate(weightedSemanticSimilarity, -1, 1, nameof(weightedSemanticSimilarity));
        Validate(tfIdfSimilarity, 0, 1, nameof(tfIdfSimilarity));
        Validate(jaccardSimilarity, 0, 1, nameof(jaccardSimilarity));

        var weights = CurrentWeights;
        return Math.Clamp(
            weightedSemanticSimilarity * weights.Semantic
            + tfIdfSimilarity * weights.TfIdf
            + jaccardSimilarity * weights.Jaccard,
            -1d,
            1d);
    }

    private static void Validate(double value, double minimum, double maximum, string parameterName)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, $"Similarity must be between {minimum} and {maximum}.");
    }
}
