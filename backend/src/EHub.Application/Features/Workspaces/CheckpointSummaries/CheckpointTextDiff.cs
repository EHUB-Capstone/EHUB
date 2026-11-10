using System.Text.RegularExpressions;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

internal sealed record DiffSegment(string Text, int Index);

internal sealed record DiffChange(DiffSegment Before, DiffSegment After, IReadOnlyList<string> BeforeFigures, IReadOnlyList<string> AfterFigures)
{
    /// <summary>True when the two versions state different numbers, which a reviewer wants to check first.</summary>
    public bool FiguresChanged => !BeforeFigures.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(AfterFigures.OrderBy(item => item, StringComparer.Ordinal));
}

/// <param name="Unchanged">Sentences found word for word in both versions.</param>
internal sealed record TextDiff(
    int Unchanged, IReadOnlyList<DiffSegment> Added, IReadOnlyList<DiffSegment> Removed, IReadOnlyList<DiffChange> Changed)
{
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
}

/// <summary>
/// Compares two versions of a document sentence by sentence, with no model involved: what is word for word the same is
/// dropped, a sentence that was reworded is paired with its old form, and what is left over was added or removed. It never
/// guesses or invents; the AI, when used at all, only phrases this result.
/// </summary>
internal static partial class CheckpointTextDiff
{
    // A sentence shorter than this is a page number, a bullet or a stray label, not content worth comparing.
    private const int MinimumSegmentCharacters = 12;
    // Reworded means mostly the same words; below this the sentences are treated as unrelated (one removed, one added).
    private const double PairingThreshold = 0.55;
    private const int MinimumTokensToPair = 4;
    // The pairing is quadratic, so a pathological document cannot stall a request.
    private const int MaximumSegmentsToPair = 500;

    [GeneratedRegex(@"(?<=[.!?…])\s+(?=\S)")]
    private static partial Regex SentenceBreak();

    [GeneratedRegex(@"\d+(?:[.,]\d+)?\s?%?")]
    private static partial Regex Number();

    [GeneratedRegex(@"[\p{L}\p{N}]{2,}")]
    private static partial Regex Word();

    public static IReadOnlyList<DiffSegment> Split(string text)
    {
        var segments = new List<DiffSegment>();
        foreach (var line in text.Split('\n'))
        {
            foreach (var part in SentenceBreak().Split(line))
            {
                var trimmed = part.Trim();
                if (trimmed.Length >= MinimumSegmentCharacters) segments.Add(new DiffSegment(trimmed, segments.Count));
            }
        }
        return segments;
    }

    public static IReadOnlyList<string> Figures(string sentence) =>
        Number().Matches(sentence).Select(match => match.Value.Replace(" ", string.Empty).Replace(',', '.')).Distinct().ToArray();

    public static TextDiff Compare(string before, string after)
    {
        var old = Split(before);
        var current = Split(after);

        // Word-for-word matches first; each old sentence can be matched once, so a repeated line is counted as often as it occurs.
        var oldByKey = old.GroupBy(item => Key(item.Text)).ToDictionary(group => group.Key, group => new Queue<DiffSegment>(group));
        var unchanged = 0;
        var added = new List<DiffSegment>();
        foreach (var segment in current)
        {
            if (oldByKey.TryGetValue(Key(segment.Text), out var queue) && queue.Count > 0) { queue.Dequeue(); unchanged++; }
            else added.Add(segment);
        }
        var removed = old.Where(item => oldByKey[Key(item.Text)].Contains(item)).ToList();

        var changed = new List<DiffChange>();
        if (added.Count > 0 && removed.Count > 0 && added.Count <= MaximumSegmentsToPair && removed.Count <= MaximumSegmentsToPair)
        {
            var addedTokens = added.ToDictionary(item => item.Index, item => Tokens(item.Text));
            var removedTokens = removed.ToDictionary(item => item.Index, item => Tokens(item.Text));
            var candidates = new List<(double Score, DiffSegment Old, DiffSegment New)>();
            foreach (var next in added)
            {
                var nextTokens = addedTokens[next.Index];
                if (nextTokens.Count < MinimumTokensToPair) continue;
                foreach (var previous in removed)
                {
                    var previousTokens = removedTokens[previous.Index];
                    if (previousTokens.Count < MinimumTokensToPair) continue;
                    var score = Jaccard(previousTokens, nextTokens);
                    if (score >= PairingThreshold) candidates.Add((score, previous, next));
                }
            }
            // Best pairs first; ties go to the sentences that sit closest to each other in the document.
            var usedOld = new HashSet<int>();
            var usedNew = new HashSet<int>();
            foreach (var pair in candidates.OrderByDescending(item => item.Score).ThenBy(item => Math.Abs(item.Old.Index - item.New.Index)))
            {
                if (usedOld.Contains(pair.Old.Index) || usedNew.Contains(pair.New.Index)) continue;
                usedOld.Add(pair.Old.Index);
                usedNew.Add(pair.New.Index);
                changed.Add(new DiffChange(pair.Old, pair.New, Figures(pair.Old.Text), Figures(pair.New.Text)));
            }
            added = added.Where(item => !usedNew.Contains(item.Index)).ToList();
            removed = removed.Where(item => !usedOld.Contains(item.Index)).ToList();
        }

        return new TextDiff(unchanged, added, removed, changed.OrderBy(item => item.After.Index).ToArray());
    }

    private static string Key(string text) => string.Join(' ', text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static HashSet<string> Tokens(string text) =>
        Word().Matches(text.ToLowerInvariant()).Select(match => match.Value).ToHashSet(StringComparer.Ordinal);

    private static double Jaccard(HashSet<string> left, HashSet<string> right)
    {
        var shared = left.Count(right.Contains);
        var total = left.Count + right.Count - shared;
        return total == 0 ? 0 : (double)shared / total;
    }
}
