namespace EHub.Application.Features.ProposalAnalyses;

public sealed record ProposalFieldSimilarityScores(
    double Problem,
    double Solution,
    double TargetCustomers,
    double ValueAndApproach);

public sealed record ProposalFieldSimilarityWeights(
    double Problem,
    double Solution,
    double TargetCustomers,
    double ValueAndApproach);

public static class WeightedSemanticSimilarity
{
    public const string CurrentVersion = "proposal-field-weighted-semantic-v1";
    public static readonly ProposalFieldSimilarityWeights CurrentWeights = new(
        Problem: 0.30,
        Solution: 0.30,
        TargetCustomers: 0.20,
        ValueAndApproach: 0.20);

    public static double Calculate(ProposalFieldSimilarityScores scores)
    {
        Validate(scores.Problem);
        Validate(scores.Solution);
        Validate(scores.TargetCustomers);
        Validate(scores.ValueAndApproach);

        var weights = CurrentWeights;
        return Math.Clamp(
            scores.Problem * weights.Problem
            + scores.Solution * weights.Solution
            + scores.TargetCustomers * weights.TargetCustomers
            + scores.ValueAndApproach * weights.ValueAndApproach,
            -1d,
            1d);
    }

    private static void Validate(double value)
    {
        if (!double.IsFinite(value) || value is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Similarity scores must be finite values between -1 and 1.");
    }
}
