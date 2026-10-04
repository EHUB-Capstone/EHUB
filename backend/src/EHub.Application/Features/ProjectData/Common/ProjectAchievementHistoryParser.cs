using System.Text.Json;
using System.Text.RegularExpressions;
using EHub.Contracts.ProjectData;

namespace EHub.Application.Features.ProjectData.Common;

internal sealed record ParsedAchievementChange(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Kept,
    bool NoteChanged,
    string? Note)
{
    internal static readonly ParsedAchievementChange Empty = new([], [], [], false, null);
}

/// <summary>
/// Writes and reads the structured part of an achievement activity-log entry. The log keeps it in
/// <c>changed_fields_json</c>, which stays a string array so other readers of the log are not affected:
/// <c>+Label</c> added, <c>-Label</c> removed, <c>=Label</c> kept, <c>note=text</c> note set,
/// <c>note-</c> note cleared. Entries written before this format are recovered from their summary sentence.
/// </summary>
internal static partial class ProjectAchievementHistoryParser
{
    private const string NoteSetPrefix = "note=";
    private const string NoteCleared = "note-";

    internal static string[] BuildTokens(
        IReadOnlyCollection<string> before,
        IReadOnlyCollection<string> after,
        bool noteChanged,
        string? note)
    {
        var tokens = new List<string>();
        tokens.AddRange(after.Except(before).Select(label => "+" + label));
        tokens.AddRange(before.Except(after).Select(label => "-" + label));
        tokens.AddRange(after.Intersect(before).Select(label => "=" + label));
        if (noteChanged) tokens.Add(note is null ? NoteCleared : NoteSetPrefix + note);
        return [.. tokens];
    }

    /// <summary>Tokens for labels (and their note) copied onto a project continuing in a later semester.</summary>
    internal static string[] BuildCarriedOverTokens(IReadOnlyCollection<string> labels, string? note)
    {
        var tokens = labels.Select(label => "+" + label).ToList();
        if (note is not null) tokens.Add(NoteSetPrefix + note);
        return [.. tokens];
    }

    internal static ParsedAchievementChange Parse(string action, string summary, string? changedFieldsJson)
    {
        var tokens = ReadTokens(changedFieldsJson);
        return tokens.Any(IsStructuredToken)
            ? ParseTokens(tokens)
            : ParseLegacy(action, summary, tokens);
    }

    private static bool IsStructuredToken(string token) =>
        token.StartsWith('+') || token.StartsWith('-') || token.StartsWith('=') ||
        token.StartsWith(NoteSetPrefix, StringComparison.Ordinal);

    private static ParsedAchievementChange ParseTokens(IEnumerable<string> tokens)
    {
        List<string> added = [], removed = [], kept = [];
        var noteChanged = false;
        string? note = null;
        foreach (var token in tokens)
        {
            if (token.StartsWith(NoteSetPrefix, StringComparison.Ordinal))
            {
                noteChanged = true;
                note = token[NoteSetPrefix.Length..];
            }
            else if (token == NoteCleared)
            {
                noteChanged = true;
            }
            else if (token.Length > 1 && ProjectAchievementMapping.Canonicalize(token[1..]) is { } label)
            {
                (token[0] switch { '+' => added, '-' => removed, _ => kept }).Add(label);
            }
        }

        return new ParsedAchievementChange(Order(added), Order(removed), Order(kept), noteChanged, note);
    }

    private static ParsedAchievementChange ParseLegacy(string action, string summary, IReadOnlyList<string> tokens)
    {
        var noteChanged = tokens.Contains("note", StringComparer.Ordinal);
        var note = noteChanged && NoteInSummary().Match(summary) is { Success: true } noteMatch
            ? noteMatch.Groups["note"].Value
            : null;

        if (action == ProjectAchievementMapping.CarriedOverAction)
        {
            var carried = tokens.Select(ProjectAchievementMapping.Canonicalize).OfType<string>().ToList();
            return new ParsedAchievementChange(Order(carried), [], [], noteChanged, note);
        }

        if (ChangeSentence().Match(summary) is not { Success: true } match)
            return new ParsedAchievementChange([], [], [], noteChanged, note);

        var before = ReadLabels(match.Groups["before"].Value);
        var after = ReadLabels(match.Groups["after"].Value);
        return new ParsedAchievementChange(
            Order(after.Except(before)), Order(before.Except(after)), Order(after.Intersect(before)), noteChanged, note);
    }

    private static List<string> ReadLabels(string text) =>
        text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ProjectAchievementMapping.Canonicalize)
            .OfType<string>()
            .ToList();

    private static string[] Order(IEnumerable<string> labels)
    {
        var set = labels.ToHashSet(StringComparer.Ordinal);
        return ProjectAchievementNames.All.Where(set.Contains).ToArray();
    }

    private static List<string> ReadTokens(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    [GeneratedRegex("^Achievements changed from (?<before>.*?) to (?<after>.*?)\\.(?: Note: .*)?$", RegexOptions.Singleline)]
    private static partial Regex ChangeSentence();

    [GeneratedRegex("Note: \"(?<note>.*)\"\\s*$", RegexOptions.Singleline)]
    private static partial Regex NoteInSummary();
}
