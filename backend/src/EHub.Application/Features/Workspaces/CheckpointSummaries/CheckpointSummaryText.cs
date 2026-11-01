using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>Pure text helpers: contact redaction before text reaches the model, chunking and quote checks.</summary>
internal static partial class CheckpointSummaryText
{
    /// <summary>Share of a quote's three-word sequences that must appear in the documents for the quote to count as grounded.</summary>
    private const double GroundedShare = 0.6;

    public static string Redact(string text)
    {
        text = Email().Replace(text, "[email]");
        text = Phone().Replace(text, "[số điện thoại]");
        return StudentId().Replace(text, "[MSSV]");
    }

    /// <summary>Splits on line breaks into pieces of at most about <paramref name="size"/> characters; a single longer line is cut.</summary>
    public static IEnumerable<string> Chunks(string text, int size)
    {
        var current = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            if (current.Length > 0 && current.Length + line.Length + 1 > size)
            {
                yield return current.ToString();
                current.Clear();
            }
            for (var offset = 0; offset < line.Length; offset += size)
            {
                var piece = line.Substring(offset, Math.Min(size, line.Length - offset));
                if (current.Length > 0 && current.Length + piece.Length > size)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                current.Append(piece);
            }
            current.Append('\n');
        }
        if (current.ToString().Trim().Length > 0) yield return current.ToString();
    }

    /// <summary>Lower-case NFC text with punctuation collapsed to single spaces, for tolerant comparisons.</summary>
    public static string Normalize(string text)
    {
        var lowered = text.Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);
        return Whitespace().Replace(NonWord().Replace(lowered, " "), " ").Trim();
    }

    public static HashSet<string> Trigrams(string normalized)
    {
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + 2 < words.Length; i++) result.Add($"{words[i]} {words[i + 1]} {words[i + 2]}");
        return result;
    }

    /// <summary>
    /// True when the quote comes from the documents. Models often merge or trim sentences, so an exact match is too
    /// strict; most of the quote's three-word sequences must still be present.
    /// </summary>
    public static bool IsGrounded(string quote, HashSet<string> sourceTrigrams)
    {
        var quoteTrigrams = Trigrams(Normalize(quote));
        if (quoteTrigrams.Count == 0) return false;
        var found = quoteTrigrams.Count(sourceTrigrams.Contains);
        return found >= GroundedShare * quoteTrigrams.Count;
    }

    /// <summary>
    /// True when the quote shares a two-word phrase with the requirement title ("gọi vốn", "đối thủ"...). Small models
    /// often quote a real sentence from another section; on the benchmark this check removed every such case, at the
    /// cost of marking many genuine matches as unclear. Precision matters more here than recall.
    /// </summary>
    public static bool IsRelevant(string requirementTitle, string quote)
    {
        var titlePairs = WordPairs(requirementTitle);
        return titlePairs.Count > 0 && WordPairs(quote).Overlaps(titlePairs);
    }

    private static readonly HashSet<string> PairStopWords = new(StringComparer.Ordinal) { "và", "hoặc", "của", "có", "các", "đã", "sau", "khi" };

    private static HashSet<string> WordPairs(string text)
    {
        var words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + 1 < words.Length; i++)
            if (!PairStopWords.Contains(words[i]) && !PairStopWords.Contains(words[i + 1])) result.Add($"{words[i]} {words[i + 1]}");
        return result;
    }

    public static string Clip(string? text, int maxLength)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd() + "…";
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<!\d)(\+84|0)\d{2,3}[\s.-]?\d{3}[\s.-]?\d{3,4}(?!\d)")]
    private static partial Regex Phone();

    // FPT-style student codes such as SE190001.
    [GeneratedRegex(@"\b[A-Z]{2}\d{6}\b")]
    private static partial Regex StudentId();

    [GeneratedRegex(@"[^\w%]+")]
    private static partial Regex NonWord();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
