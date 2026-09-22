using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EHub.Contracts.ProjectProposals;

namespace EHub.Application.Features.ProposalAnalyses;

public sealed record CanonicalProposalText(
    string Text,
    string ContentHash,
    string SchemaVersion,
    bool WasTruncated);

public interface IProposalEmbeddingTextBuilder
{
    CanonicalProposalText Build(ProjectProposalSnapshotDto proposal);
}

public sealed partial class ProposalEmbeddingTextBuilder : IProposalEmbeddingTextBuilder
{
    public const string CurrentSchemaVersion = "proposal-embedding-text-v1";
    public const int MaximumCharacters = 24_000;

    public CanonicalProposalText Build(ProjectProposalSnapshotDto proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var fields = new (string Label, string? Value)[]
        {
            ("TITLE", proposal.Title),
            ("STARTUP_NAME", proposal.StartupName),
            ("TAGLINE", proposal.Tagline),
            ("PROBLEM", proposal.Problem),
            ("SOLUTION", proposal.Solution),
            ("TARGET_CUSTOMERS", proposal.TargetCustomers),
            ("VALUE_PROPOSITION", proposal.ValueProposition),
            ("MARKET_SIZE", proposal.MarketSize),
            ("COMPETITORS", proposal.Competitors),
            ("BUSINESS_MODEL", proposal.BusinessModel),
            ("REVENUE_MODEL", proposal.RevenueModel),
            ("MARKETING_STRATEGY", proposal.MarketingStrategy),
            ("TECHNOLOGY", proposal.Technology),
            ("FINANCIAL_PLAN", proposal.FinancialPlan),
            ("ROADMAP", proposal.Roadmap),
            ("TEAM_INTRODUCTION", proposal.TeamIntroduction)
        };

        var builder = new StringBuilder();
        foreach (var (label, value) in fields)
        {
            var normalized = Normalize(value);
            if (normalized.Length == 0) continue;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(label).Append(": ").Append(normalized);
        }

        var text = builder.ToString();
        var wasTruncated = text.Length > MaximumCharacters;
        if (wasTruncated)
            text = text[..MaximumCharacters].TrimEnd();

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        return new CanonicalProposalText(text, hash, CurrentSchemaVersion, wasTruncated);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return WhitespaceRegex().Replace(value.Normalize(NormalizationForm.FormC), " ").Trim();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
