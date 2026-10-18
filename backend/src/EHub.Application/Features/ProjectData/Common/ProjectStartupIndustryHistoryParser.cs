using System.Text.Json;

namespace EHub.Application.Features.ProjectData.Common;

internal sealed record ParsedStartupIndustryChange(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Kept)
{
    internal static readonly ParsedStartupIndustryChange Empty = new([], [], []);
}

/// <summary>
/// Writes and reads the structured part of a startup-industry activity-log entry. Like the achievement history it
/// lives in <c>changed_fields_json</c> as a string array: <c>+Name</c> added, <c>-Name</c> removed, <c>=Name</c> kept.
/// </summary>
internal static class ProjectStartupIndustryHistoryParser
{
    internal const string ChangedAction = "PROJECT_STARTUP_INDUSTRIES_CHANGED";

    internal static string[] BuildTokens(IReadOnlyCollection<string> before, IReadOnlyCollection<string> after)
    {
        var tokens = new List<string>();
        tokens.AddRange(after.Except(before, StringComparer.OrdinalIgnoreCase).Select(name => "+" + name));
        tokens.AddRange(before.Except(after, StringComparer.OrdinalIgnoreCase).Select(name => "-" + name));
        tokens.AddRange(after.Intersect(before, StringComparer.OrdinalIgnoreCase).Select(name => "=" + name));
        return [.. tokens];
    }

    internal static ParsedStartupIndustryChange Parse(string? changedFieldsJson)
    {
        if (string.IsNullOrWhiteSpace(changedFieldsJson)) return ParsedStartupIndustryChange.Empty;

        List<string> tokens;
        try
        {
            tokens = JsonSerializer.Deserialize<List<string>>(changedFieldsJson) ?? [];
        }
        catch (JsonException)
        {
            return ParsedStartupIndustryChange.Empty;
        }

        List<string> added = [], removed = [], kept = [];
        foreach (var token in tokens.Where(token => token.Length > 1))
        {
            var name = token[1..];
            switch (token[0])
            {
                case '+': added.Add(name); break;
                case '-': removed.Add(name); break;
                case '=': kept.Add(name); break;
            }
        }

        return new ParsedStartupIndustryChange(Sort(added), Sort(removed), Sort(kept));
    }

    private static string[] Sort(IEnumerable<string> names) => ProjectDataQuery.DistinctSorted(names);
}
