using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>Pure text helpers: contact redaction before text reaches the model, sentence selection and quotes.</summary>
internal static partial class CheckpointSummaryText
{
    public static string Redact(string text)
    {
        text = Email().Replace(text, "[email]");
        text = Phone().Replace(text, "[số điện thoại]");
        return StudentId().Replace(text, "[MSSV]");
    }

    private static readonly string[] Keywords =
    [
        "khách hàng", "doanh thu", "giá", "chi phí", "đối thủ", "rủi ro", "vốn", "mvp", "prototype", "khảo sát",
        "phỏng vấn", "kết quả", "mục tiêu", "giải pháp", "vấn đề", "kênh", "marketing", "lợi nhuận", "thị trường",
        "người dùng", "kế hoạch", "tài chính", "hòa vốn", "đơn", "tỷ lệ"
    ];

    /// <summary>Lines, with long lines split into sentences. Headings and slide bullets stay whole.</summary>
    public static IReadOnlyList<string> Sentences(string text)
    {
        var result = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.Length <= 200) { result.Add(line); continue; }
            result.AddRange(SentenceEnd().Split(line).Select(item => item.Trim()).Where(item => item.Length > 0));
        }
        return result;
    }

    /// <summary>
    /// Shrinks a long document to about <paramref name="budget"/> characters without a model, so a single model call
    /// can read it. Sentences with numbers, requirement phrases, domain keywords, headings and the opening of the
    /// document score higher; near-duplicates (repeated logs) are pushed down; the original order is kept.
    /// On the benchmark this replaced several chunk-summary calls and cut a 20,000-character report from 506 s to 128 s.
    /// </summary>
    public static string Select(string text, IReadOnlyCollection<string> requirementTitles, int budget)
    {
        if (text.Length <= budget) return text;
        var scored = Score(Sentences(text), requirementTitles);
        var chosen = new HashSet<int>();
        var used = 0;
        foreach (var item in scored.OrderByDescending(item => item.Score).ThenBy(item => item.Position))
        {
            if (used + item.Text.Length + 1 > budget) continue;
            chosen.Add(item.Position);
            used += item.Text.Length + 1;
        }
        return string.Join('\n', scored.Where(item => chosen.Contains(item.Position)).Select(item => item.Text));
    }

    /// <summary>
    /// A file's most informative full sentences (not headings), quoted as written, in document order. Chosen without the
    /// model, so every file in a checkpoint gets its own preview at no extra model time.
    /// </summary>
    public static IReadOnlyList<string> Highlights(string text, IReadOnlyCollection<string> requirementTitles, int count)
    {
        var scored = Score(Sentences(text), requirementTitles);
        return scored
            .Where(item => item.Text.Length is >= 30 and <= 300 && !IsHeading(item.Text))
            .OrderByDescending(item => item.Score).ThenBy(item => item.Position)
            .Take(count)
            .OrderBy(item => item.Position)
            .Select(item => item.Text)
            .ToArray();
    }

    private static List<(double Score, int Position, string Text)> Score(IReadOnlyList<string> sentences, IReadOnlyCollection<string> requirementTitles)
    {
        var requirementPairs = new HashSet<string>(requirementTitles.SelectMany(WordPairs), StringComparer.Ordinal);
        var seen = new List<HashSet<string>>();
        var scored = new List<(double Score, int Position, string Text)>(sentences.Count);
        for (var position = 0; position < sentences.Count; position++)
        {
            var sentence = sentences[position];
            var lowered = sentence.ToLower(CultureInfo.InvariantCulture);
            var score = 0.0;
            if (sentence.Any(char.IsDigit)) score += 3;
            if (requirementPairs.Overlaps(WordPairs(sentence))) score += 3;
            score += Math.Min(3, Keywords.Count(keyword => lowered.Contains(keyword, StringComparison.Ordinal)));
            if (IsHeading(sentence)) score += 1.5;
            if (position < sentences.Count * 0.1) score += 1;
            var signature = Trigrams(Normalize(sentence));
            if (signature.Count > 0 && seen.Any(other => signature.Count(other.Contains) > 0.6 * signature.Count)) score -= 4;
            seen.Add(signature);
            scored.Add((score, position, sentence));
        }
        return scored;
    }


    /// <summary>
    /// The source sentence sharing the most two-word phrases with the requirement title ("gọi vốn", "đối thủ"...), or
    /// null if none does. When the best match is a section heading, the first sentence under it is returned instead,
    /// because the heading alone says nothing. The backend picks the quote so it is always real text, and it must name
    /// the requirement: small models often point at a real sentence from another section.
    /// </summary>
    public static string? BestEvidence(string requirementTitle, IReadOnlyList<string> sentences)
    {
        var index = BestEvidenceIndex(requirementTitle, sentences);
        return index < 0 ? null : sentences[index];
    }

    /// <summary>Like <see cref="BestEvidence"/>, but returns the position so the caller knows which file the quote is from.</summary>
    public static int BestEvidenceIndex(string requirementTitle, IReadOnlyList<string> sentences)
    {
        var titlePairs = WordPairs(requirementTitle);
        if (titlePairs.Count == 0) return -1;
        var bestIndex = -1;
        var bestCount = 0;
        for (var i = 0; i < sentences.Count; i++)
        {
            var count = WordPairs(sentences[i]).Count(titlePairs.Contains);
            // Prefer content over headings on a tie.
            if (count > bestCount || (count == bestCount && count > 0 && IsHeading(sentences[bestIndex]) && !IsHeading(sentences[i])))
            {
                bestIndex = i;
                bestCount = count;
            }
        }
        if (bestIndex < 0) return -1;
        return IsHeading(sentences[bestIndex]) && bestIndex + 1 < sentences.Count && !IsHeading(sentences[bestIndex + 1])
            ? bestIndex + 1
            : bestIndex;
    }

    private static bool IsHeading(string sentence) =>
        sentence.Length <= 80 && !sentence.EndsWith('.') && sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 10;

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

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceEnd();
}
