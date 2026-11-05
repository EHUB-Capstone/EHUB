using System.Text.Json;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// What a rubric criterion needs in order to be judged, worked out from its name. Many criteria of the EXE101 and EXE201
/// rubrics are not about the documents at all: a presentation, a Q&amp;A or a team's coordination is assessed live, and an
/// MVP or a launched product is assessed by looking at the product. Telling reviewers "not found in the files" for these
/// would suggest the team left something out, so they are marked instead.
/// </summary>
internal static partial class CheckpointCriterionProfile
{
    /// <summary>A submitted document can show it: roadmap, persona, business model, sales report...</summary>
    public const string Document = "Document";

    /// <summary>Judged on the product or demo; the files may mention it but cannot prove it.</summary>
    public const string Product = "Product";

    /// <summary>Judged live (presentation, Q&amp;A, teamwork) or over time (effort); no file can show it.</summary>
    public const string Live = "Live";

    /// <summary>
    /// Judged on how clear or complete the whole submission is ("Startup Idea Clarity", "Startup Completeness"), so no single
    /// sentence is "the" evidence. Evidence is quoted when there is some; otherwise the criterion is not reported as missing.
    /// </summary>
    public const string Overall = "Overall";

    public static string Classify(string title)
    {
        // Live first: "Product Understanding & Q&A" is a Q&A, "Presentation & Demo" a presentation.
        if (LiveName().IsMatch(title)) return Live;
        if (ProductName().IsMatch(title)) return Product;
        return OverallName().IsMatch(title) ? Overall : Document;
    }

    /// <summary>The Excellent level of an imported rubric ("levels" JSON of the criterion), or null.</summary>
    public static string? ExcellentText(string? levelsJson)
    {
        if (string.IsNullOrWhiteSpace(levelsJson)) return null;
        try
        {
            using var document = JsonDocument.Parse(levelsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
            string? first = null;
            foreach (var level in document.RootElement.EnumerateArray())
            {
                if (level.ValueKind != JsonValueKind.Object || !level.TryGetProperty("description", out var description) ||
                    description.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(description.GetString()))
                    continue;
                first ??= description.GetString()!.Trim();
                var key = level.TryGetProperty("key", out var keyElement) && keyElement.ValueKind == JsonValueKind.String
                    ? keyElement.GetString() : null;
                if (string.Equals(key, "excellent", StringComparison.OrdinalIgnoreCase)) return description.GetString()!.Trim();
            }
            // Levels are listed best first, so without an "excellent" key the first one is still the best.
            return first;
        }
        catch (JsonException) { return null; }
    }

    [GeneratedRegex(
        @"presentation|pitch|storytelling|q\s?&\s?a|discussion|demo skills|teamwork|coordination|defen[cs]e|progress\s?&\s?effort|\beffort\b|thuyết trình|hỏi đáp|làm việc nhóm|bảo vệ",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LiveName();

    [GeneratedRegex(@"idea clarity|startup completeness|ý tưởng rõ ràng", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OverallName();

    [GeneratedRegex(
        @"\bmvp\b|prototype|product completeness|product output|product quality|sản phẩm hoàn chỉnh|bản mẫu",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProductName();
}
