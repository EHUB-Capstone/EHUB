using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Enums;

namespace EHub.Application.Features.ProposalAnalyses;

public sealed record CanonicalProposalFieldText(
    ProjectProposalSemanticField Field,
    string Text,
    string ContentHash,
    string SchemaVersion);

public interface IProposalFieldEmbeddingTextBuilder
{
    IReadOnlyList<CanonicalProposalFieldText> Build(ProjectProposalSnapshotDto proposal);
}

public sealed partial class ProposalFieldEmbeddingTextBuilder : IProposalFieldEmbeddingTextBuilder
{
    public const string CurrentSchemaVersion = "proposal-field-embedding-text-v1";
    private const int MaximumFieldCharacters = 8_000;

    public IReadOnlyList<CanonicalProposalFieldText> Build(ProjectProposalSnapshotDto proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return
        [
            BuildField(ProjectProposalSemanticField.Problem, [("PROBLEM", proposal.Problem)]),
            BuildField(ProjectProposalSemanticField.Solution, [("SOLUTION", proposal.Solution)]),
            BuildField(ProjectProposalSemanticField.TargetCustomers, [("TARGET_CUSTOMERS", proposal.TargetCustomers)]),
            BuildField(ProjectProposalSemanticField.ValueAndApproach,
            [
                ("VALUE_PROPOSITION", proposal.ValueProposition),
                ("BUSINESS_MODEL", proposal.BusinessModel),
                ("TECHNOLOGY", proposal.Technology)
            ])
        ];
    }

    private static CanonicalProposalFieldText BuildField(
        ProjectProposalSemanticField field,
        IReadOnlyList<(string Label, string? Value)> values)
    {
        var builder = new StringBuilder();
        foreach (var (label, value) in values)
        {
            var normalized = Normalize(value);
            if (normalized.Length == 0) continue;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(label).Append(": ").Append(normalized);
        }

        var text = builder.ToString();
        if (text.Length > MaximumFieldCharacters)
            text = text[..MaximumFieldCharacters].TrimEnd();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        return new CanonicalProposalFieldText(field, text, hash, CurrentSchemaVersion);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return WhitespaceRegex().Replace(value.Normalize(NormalizationForm.FormC), " ").Trim();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
