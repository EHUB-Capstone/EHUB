using System.Text;
using System.Text.RegularExpressions;
using EHub.Contracts.ProjectProposals;

namespace EHub.Application.Features.ProposalAnalyses;

public sealed record ProposalLexicalSimilarityScores(
    double TfIdf,
    double Jaccard);

public static partial class ProposalLexicalSimilarity
{
    public const string CurrentVersion = "proposal-lexical-unigram-bigram-v1";

    public static IReadOnlyList<ProposalLexicalSimilarityScores> Calculate(
        ProjectProposalSnapshotDto current,
        IReadOnlyList<ProjectProposalSnapshotDto> candidates)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
            return [];

        var documents = new List<FeatureDocument>(candidates.Count + 1)
        {
            BuildDocument(current)
        };
        documents.AddRange(candidates.Select(BuildDocument));

        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            foreach (var feature in document.Counts.Keys)
                documentFrequency[feature] = documentFrequency.GetValueOrDefault(feature) + 1;
        }

        var idf = documentFrequency.ToDictionary(
            pair => pair.Key,
            pair => Math.Log((documents.Count + 1d) / (pair.Value + 1d)) + 1d,
            StringComparer.Ordinal);
        var currentVector = BuildTfIdfVector(documents[0], idf);

        return documents
            .Skip(1)
            .Select(document => new ProposalLexicalSimilarityScores(
                CalculateCosine(currentVector, BuildTfIdfVector(document, idf)),
                CalculateJaccard(documents[0].Counts.Keys, document.Counts.Keys)))
            .ToArray();
    }

    private static FeatureDocument BuildDocument(ProjectProposalSnapshotDto proposal)
    {
        var values = new[]
        {
            proposal.Title,
            proposal.StartupName,
            proposal.Tagline,
            proposal.Problem,
            proposal.Solution,
            proposal.TargetCustomers,
            proposal.ValueProposition,
            proposal.MarketSize,
            proposal.Competitors,
            proposal.BusinessModel,
            proposal.RevenueModel,
            proposal.MarketingStrategy,
            proposal.Technology,
            proposal.FinancialPlan,
            proposal.Roadmap,
            proposal.TeamIntroduction
        };

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var tokens = TokenRegex()
                .Matches(value.Normalize(NormalizationForm.FormC))
                .Select(match => match.Value.ToLowerInvariant())
                .ToArray();
            for (var index = 0; index < tokens.Length; index++)
            {
                Add(counts, $"u:{tokens[index]}");
                if (index + 1 < tokens.Length)
                    Add(counts, $"b:{tokens[index]}\u001f{tokens[index + 1]}");
            }
        }

        return new FeatureDocument(counts);
    }

    private static IReadOnlyDictionary<string, double> BuildTfIdfVector(
        FeatureDocument document,
        IReadOnlyDictionary<string, double> idf)
    {
        var total = document.Counts.Values.Sum();
        if (total == 0)
            return new Dictionary<string, double>(StringComparer.Ordinal);

        return document.Counts.ToDictionary(
            pair => pair.Key,
            pair => pair.Value / (double)total * idf[pair.Key],
            StringComparer.Ordinal);
    }

    private static double CalculateCosine(
        IReadOnlyDictionary<string, double> left,
        IReadOnlyDictionary<string, double> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return 0;

        var dotProduct = left.Sum(pair => pair.Value * right.GetValueOrDefault(pair.Key));
        var leftMagnitude = Math.Sqrt(left.Values.Sum(value => value * value));
        var rightMagnitude = Math.Sqrt(right.Values.Sum(value => value * value));
        if (leftMagnitude == 0 || rightMagnitude == 0)
            return 0;

        return Math.Clamp(dotProduct / (leftMagnitude * rightMagnitude), 0d, 1d);
    }

    private static double CalculateJaccard(IEnumerable<string> left, IEnumerable<string> right)
    {
        var leftSet = left.ToHashSet(StringComparer.Ordinal);
        var rightSet = right.ToHashSet(StringComparer.Ordinal);
        if (leftSet.Count == 0 && rightSet.Count == 0)
            return 0;

        var intersection = leftSet.Count(rightSet.Contains);
        var union = leftSet.Count + rightSet.Count - intersection;
        return intersection / (double)union;
    }

    private static void Add(IDictionary<string, int> counts, string feature)
    {
        counts.TryGetValue(feature, out var count);
        counts[feature] = count + 1;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    private sealed record FeatureDocument(IReadOnlyDictionary<string, int> Counts);
}
