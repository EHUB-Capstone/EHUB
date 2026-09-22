using System.Text;
using System.Text.RegularExpressions;
using EHub.Application.Common.Interfaces.AI;
using EHub.Contracts.ProjectProposals;

namespace EHub.Application.Features.ProposalAnalyses;

public static partial class ProposalAnalysisOutputValidator
{
    private const int MaximumItems = 5;
    private const int MaximumItemLength = 500;

    public static void Validate(
        ProposalAnalysisProviderRequest request,
        ProposalAnalysisProviderResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(response.Summary) || response.Summary.Trim().Length > 2_000
            || !Enum.IsDefined(response.OverlapRisk)
            || !ValidItems(response.PotentialDifferentiators, requireOne: true)
            || !ValidItems(response.Limitations, requireOne: true)
            || !ValidMetadata(response.Provider)
            || !ValidMetadata(response.Model)
            || !ValidMetadata(response.PromptVersion)
            || !ValidMetadata(response.OutputSchemaVersion)
            || ContainsForbiddenVerdict(AllNarrativeText(response)))
        {
            throw InvalidOutput();
        }

        var expectedCandidates = request.RetrievalCandidates.ToDictionary(candidate => candidate.ProposalVersionId);
        if (response.Matches.Count != expectedCandidates.Count
            || response.Matches.Select(match => match.ProposalVersionId).Distinct().Count() != response.Matches.Count
            || response.Matches.Any(match => !expectedCandidates.ContainsKey(match.ProposalVersionId)))
        {
            throw InvalidOutput();
        }

        foreach (var match in response.Matches)
        {
            if (!ValidItems(match.Similarities, requireOne: true)
                || !ValidItems(match.Differences, requireOne: true)
                || !ValidItems(match.NovelElements, requireOne: true)
                || match.Evidence is not { Count: >= 1 and <= MaximumItems })
            {
                throw InvalidOutput();
            }

            var candidate = expectedCandidates[match.ProposalVersionId];
            foreach (var evidence in match.Evidence)
            {
                if (string.IsNullOrWhiteSpace(evidence.Quote)
                    || evidence.Quote.Trim().Length > MaximumItemLength
                    || !Enum.IsDefined(evidence.Source)
                    || !EvidenceExists(
                        evidence.Source == ProposalAnalysisEvidenceSource.Current
                            ? request.Proposal
                            : candidate.Proposal,
                        evidence.Quote))
                {
                    throw InvalidOutput();
                }
            }
        }
    }

    private static IEnumerable<string> AllNarrativeText(ProposalAnalysisProviderResponse response) =>
        new[] { response.Summary }
            .Concat(response.PotentialDifferentiators)
            .Concat(response.Limitations)
            .Concat(response.Matches.SelectMany(match => match.Similarities))
            .Concat(response.Matches.SelectMany(match => match.Differences))
            .Concat(response.Matches.SelectMany(match => match.NovelElements));

    private static bool ContainsForbiddenVerdict(IEnumerable<string> values) =>
        values.Any(value => ForbiddenVerdictRegex().IsMatch(value));

    private static bool EvidenceExists(ProjectProposalSnapshotDto proposal, string quote)
    {
        var normalizedQuote = Normalize(quote);
        if (normalizedQuote.Length == 0)
            return false;

        return ProposalValues(proposal)
            .Select(Normalize)
            .Any(value => value.Contains(normalizedQuote, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ProposalValues(ProjectProposalSnapshotDto proposal)
    {
        yield return proposal.Title;
        yield return proposal.StartupName;
        yield return proposal.Tagline;
        yield return proposal.Problem;
        yield return proposal.Solution;
        yield return proposal.TargetCustomers;
        yield return proposal.ValueProposition;
        yield return proposal.MarketSize;
        yield return proposal.Competitors;
        yield return proposal.BusinessModel;
        yield return proposal.RevenueModel;
        yield return proposal.MarketingStrategy;
        yield return proposal.Technology;
        yield return proposal.FinancialPlan;
        yield return proposal.Roadmap;
        yield return proposal.TeamIntroduction;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Normalize(NormalizationForm.FormC), " ").Trim();

    private static bool ValidItems(IReadOnlyCollection<string>? items, bool requireOne) =>
        items != null
        && (!requireOne || items.Count > 0)
        && items.Count <= MaximumItems
        && items.All(item => !string.IsNullOrWhiteSpace(item) && item.Trim().Length <= MaximumItemLength);

    private static bool ValidMetadata(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100;

    private static ProposalAnalysisProcessingException InvalidOutput() => new(
        "PROPOSAL_ANALYSIS_OUTPUT_INVALID",
        "The analysis provider returned an invalid result.",
        isTransient: true);

    [GeneratedRegex(
        @"\b(?:recommend(?:s|ed|ation)?|should|must)\s+(?:be\s+)?(?:approve|approved|reject|rejected)\b|(?:nên|đề nghị|khuyến nghị)\s+(?:phê duyệt|chấp thuận|từ chối)\b|\b(?:is|constitutes)\s+plagiarism\b|\blà\s+đạo\s+văn\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenVerdictRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
