using System.Text.Json;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Validates the model's JSON for one file and turns it into the stored content. The model is not trusted: lengths
/// are clipped and it never writes quotes. For each requirement the backend quotes a sentence of the file that names
/// it, and it picks the file's highlights itself.
/// </summary>
internal static class CheckpointSummaryOutput
{
    public const string Related = "Related";
    public const string NotFound = "NotFound";
    public const string Unclear = "Unclear";
    public const string NotChecked = "NotChecked";

    private const int HighlightsPerFile = 3;

    public static WorkspaceFileSummaryContent? TryBuild(string json, string fileText, IReadOnlyList<SummarySourceRequirement> requirements)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryString(root, "overview", out var overview) ||
                string.IsNullOrWhiteSpace(overview) || !root.TryGetProperty("requirements", out var requirementItems) ||
                requirementItems.ValueKind != JsonValueKind.Array)
                return null;

            var sentences = CheckpointSummaryText.Sentences(fileText);
            var findings = new Dictionary<int, (string Finding, string? Evidence)>();
            foreach (var item in requirementItems.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("index", out var indexElement) ||
                    indexElement.ValueKind != JsonValueKind.Number || !indexElement.TryGetInt32(out var number) ||
                    !TryString(item, "status", out var status)) return null;
                var index = number - 1;
                if (index < 0 || index >= requirements.Count || findings.ContainsKey(index)) continue;
                if (status is not ("Covered" or "Partial"))
                {
                    findings[index] = (NotFound, null);
                    continue;
                }
                // The model says the requirement is addressed; only a sentence that names it can back that up.
                var evidence = CheckpointSummaryText.BestEvidence(requirements[index].Title, sentences);
                findings[index] = evidence is null
                    ? (Unclear, null)
                    : (Related, CheckpointSummaryText.Clip(evidence, CheckpointSummaryLimits.MaxEvidenceCharacters));
            }

            var titles = requirements.Select(item => item.Title).ToArray();
            return new WorkspaceFileSummaryContent
            {
                Overview = CheckpointSummaryText.Clip(overview, CheckpointSummaryLimits.MaxOverviewCharacters),
                KeyPoints = Clean(Strings(root, "keyPoints"), CheckpointSummaryLimits.MaxKeyPoints),
                Questions = Clean(Strings(root, "questions"), CheckpointSummaryLimits.MaxQuestions),
                Highlights = CheckpointSummaryText.Highlights(fileText, titles, HighlightsPerFile)
                    .Select(text => CheckpointSummaryText.Clip(text, CheckpointSummaryLimits.MaxEvidenceCharacters))
                    .ToArray(),
                Requirements = requirements.Select(requirement =>
                {
                    var (finding, evidence) = findings.GetValueOrDefault(requirement.Index, (Unclear, null));
                    return new WorkspaceFileRequirementFinding
                    {
                        Index = requirement.Index, Title = requirement.Title, Finding = finding, Evidence = evidence
                    };
                }).ToArray()
            };
        }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Combines the per-file findings, newest file first: Related wins (with the first file that has a quote), then
    /// Unclear, then NotFound. Requirements no summarised file looked at are NotChecked.
    /// </summary>
    public static IReadOnlyList<WorkspaceCheckpointRequirementFinding> Combine(
        IReadOnlyList<SummarySourceRequirement> requirements, IReadOnlyList<(string FileName, WorkspaceFileSummaryContent Content)> files)
    {
        return requirements.Select(requirement =>
        {
            var matches = files
                .Select(file => (file.FileName, Finding: file.Content.Requirements.FirstOrDefault(item => item.Index == requirement.Index)))
                .Where(item => item.Finding is not null)
                .ToArray();
            var related = matches.FirstOrDefault(item => item.Finding!.Finding == Related);
            var finding = related.Finding is not null ? Related
                : matches.Any(item => item.Finding!.Finding == Unclear) ? Unclear
                : matches.Length > 0 ? NotFound
                : NotChecked;
            return new WorkspaceCheckpointRequirementFinding
            {
                Index = requirement.Index,
                Title = requirement.Title,
                Finding = finding,
                Evidence = related.Finding?.Evidence,
                Source = related.Finding is null ? null : related.FileName
            };
        }).ToArray();
    }

    public static string Domain(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;

    private static string[] Clean(IEnumerable<string> values, int max) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => CheckpointSummaryText.Clip(value, CheckpointSummaryLimits.MaxItemCharacters))
            .Distinct(StringComparer.Ordinal)
            .Take(max)
            .ToArray();

    private static IEnumerable<string> Strings(JsonElement root, string name) =>
        root.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)
            : [];

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString() ?? string.Empty;
        return true;
    }
}
