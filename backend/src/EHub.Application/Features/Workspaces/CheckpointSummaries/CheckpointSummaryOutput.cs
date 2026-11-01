using System.Text.Json;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <param name="Text">The extracted text, or null when the file was not read.</param>
/// <param name="Status">Summarized, Unreadable or NotIncluded.</param>
internal sealed record SummaryFileText(SummarySourceFile File, string? Text, string Status);

/// <summary>
/// Validates the model's JSON and turns it into the response content. The model is not trusted: lengths are clipped,
/// unknown files are ignored, and a requirement is only reported as related when its quote is found in the documents.
/// </summary>
internal static class CheckpointSummaryOutput
{
    public const string Related = "Related";
    public const string NotFound = "NotFound";
    public const string Unclear = "Unclear";

    public const string FileSummarized = "Summarized";
    public const string FileUnreadable = "Unreadable";
    public const string FileNotIncluded = "NotIncluded";

    public static bool TryParseMap(string json, out string summary, out IReadOnlyList<string> quotes)
    {
        summary = string.Empty;
        quotes = [];
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryString(root, "summary", out summary)) return false;
            quotes = Strings(root, "quotes").Take(6).ToArray();
            return true;
        }
        catch (JsonException) { return false; }
    }

    public static WorkspaceCheckpointSummaryContent? TryBuild(
        string json,
        IReadOnlyList<SummaryFileText> files,
        IReadOnlyList<SummarySourceLink> links,
        IReadOnlyList<SummarySourceRequirement> requirements,
        string sourceText)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryString(root, "overview", out var overview) ||
                string.IsNullOrWhiteSpace(overview) || !root.TryGetProperty("requirements", out var requirementItems) ||
                requirementItems.ValueKind != JsonValueKind.Array)
                return null;

            var sourceTrigrams = CheckpointSummaryText.Trigrams(CheckpointSummaryText.Normalize(sourceText));
            var findings = new Dictionary<int, (string Finding, string? Evidence)>();
            foreach (var item in requirementItems.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("index", out var indexElement) ||
                    indexElement.ValueKind != JsonValueKind.Number || !indexElement.TryGetInt32(out var number) ||
                    !TryString(item, "status", out var status)) return null;
                var index = number - 1;
                if (index < 0 || index >= requirements.Count || findings.ContainsKey(index)) continue;
                TryString(item, "evidence", out var evidence);
                var claimed = status is "Covered" or "Partial";
                var grounded = claimed && CheckpointSummaryText.IsGrounded(evidence, sourceTrigrams);
                var quote = grounded ? CheckpointSummaryText.Clip(evidence, CheckpointSummaryLimits.MaxEvidenceCharacters) : null;
                findings[index] = status == "NotFound"
                    ? (NotFound, null)
                    // A real quote that does not mention the requirement is shown, but only as unclear; an invented
                    // quote is never shown.
                    : grounded && CheckpointSummaryText.IsRelevant(requirements[index].Title, evidence)
                        ? (Related, quote)
                        : (Unclear, quote);
            }

            var fileSummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("files", out var fileItems) && fileItems.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in fileItems.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && TryString(item, "name", out var name) &&
                        TryString(item, "summary", out var summary) && !string.IsNullOrWhiteSpace(summary))
                        fileSummaries.TryAdd(name.Trim(), summary);
                }
            }

            return new WorkspaceCheckpointSummaryContent
            {
                Overview = CheckpointSummaryText.Clip(overview, CheckpointSummaryLimits.MaxOverviewCharacters),
                KeyPoints = Clean(Strings(root, "keyPoints"), CheckpointSummaryLimits.MaxKeyPoints),
                Questions = Clean(Strings(root, "questions"), CheckpointSummaryLimits.MaxQuestions),
                Files = files.Select(item => new WorkspaceCheckpointSummaryFile
                {
                    FileId = item.File.Id,
                    Name = item.File.OriginalName,
                    Status = item.Status,
                    Summary = item.Status == FileSummarized && fileSummaries.TryGetValue(item.File.OriginalName, out var summary)
                        ? CheckpointSummaryText.Clip(summary, CheckpointSummaryLimits.MaxItemCharacters)
                        : null
                }).ToArray(),
                Links = links.Select(link => new WorkspaceCheckpointSummaryLink { Name = link.Name, Domain = Domain(link.Url) }).ToArray(),
                Requirements = requirements.Select(requirement =>
                {
                    var (finding, evidence) = findings.GetValueOrDefault(requirement.Index, (Unclear, null));
                    return new WorkspaceCheckpointSummaryRequirement
                    {
                        Index = requirement.Index, Title = requirement.Title, Finding = finding, Evidence = evidence
                    };
                }).ToArray()
            };
        }
        catch (JsonException) { return null; }
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
