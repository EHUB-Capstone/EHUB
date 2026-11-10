using System.Text;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Repairs text read from submitted files before it is quoted or sent to the model. PDFs exported from slides are the
/// main concern: their fonts often place Vietnamese tone marks as separate glyphs (so "TIẾP" arrives as "TIÊ P"),
/// animated slides repeat their titles ("2.PROJECT2.PROJECT"), and sentences are broken at every visual line.
/// </summary>
internal static partial class CheckpointLayoutText
{
    // Vowels that carry a Vietnamese diacritic. A syllable split right after one of them is the PDF artefact; a split
    // after a plain ASCII letter is an ordinary space between words.
    private const string MarkedVowels =
        "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ";

    private static readonly HashSet<string> ConsonantEndings = new(StringComparer.Ordinal)
        { "c", "ch", "m", "n", "ng", "nh", "p", "t" };

    // Vowel endings ("ĐÂ Y" for "ĐẦY") are joined only in upper case: in lower case "y", "o"... are also whole words.
    private static readonly HashSet<string> VowelEndings = new(StringComparer.Ordinal) { "i", "y", "o", "u" };

    private const int ShortLine = 60;
    private const int CapitalParagraphLine = 30;

    /// <param name="layoutText">
    /// True for PDF and slides, whose lines follow the page layout: repeated titles are dropped and broken sentences
    /// are joined. Word documents keep their paragraphs, where a repeated short line can be real table content.
    /// </param>
    public static string Tidy(string text, bool layoutText)
    {
        // Tone marks sent as separate code points ("a" + a combining accent) would hide the vowel from the repairs below.
        var lines = text.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Split('\n')
            .Select(line => JoinSplitSyllables(Whitespace().Replace(line, " ").Trim()))
            .Where(line => line.Length > 0);
        if (!layoutText) return string.Join('\n', lines);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();
        foreach (var raw in lines)
        {
            var line = CollapseRepeat(raw);
            // Titles repeated on every slide of a section say nothing new the second time.
            if (line.Length <= ShortLine && !seen.Add(line)) continue;
            kept.Add(line);
        }
        // Joining lines can bring a split syllable together ("Quy mô/gi" + "á" + "trị"), so they are repaired again.
        return string.Join('\n', JoinBrokenLines(kept).Select(JoinSplitSyllables));
    }

    // A line ending on one of these cannot be a whole sentence ("…nhân sự mới bằng" / "Scenario Role-play."), even when the
    // next line starts in capitals.
    private static readonly HashSet<string> OpenEndings = new(StringComparer.OrdinalIgnoreCase)
    {
        "bằng", "và", "của", "cho", "để", "với", "trong", "các", "những", "một", "hoặc", "như", "theo", "từ", "về", "tại", "là",
        "the", "of", "and", "or", "to", "for", "with", "in", "a", "an", "by", "on"
    };

    private static bool EndsOpen(string line)
    {
        var start = line.LastIndexOf(' ') + 1;
        return OpenEndings.Contains(line[start..]);
    }

    /// <summary>"TIÊ P" → "TIÊP", "hế t" → "hết". The lost tone mark cannot be recovered, but the word is whole again.</summary>
    internal static string JoinSplitSyllables(string line) =>
        LoneVowel().Replace(SplitSyllable().Replace(line, match =>
        {
            var vowel = match.Groups["vowel"].Value[0];
            var ending = match.Groups["ending"].Value;
            if (!MarkedVowels.Contains(char.ToLowerInvariant(vowel))) return match.Value;
            var upper = char.IsUpper(vowel);
            if (upper ? ending != ending.ToUpperInvariant() : ending != ending.ToLowerInvariant()) return match.Value;
            var lower = ending.ToLowerInvariant();
            // "đầ u" for "đầu": a lone "u" never stands as a Vietnamese word, unlike "i", "y" and "o".
            var joins = ConsonantEndings.Contains(lower) || (upper && VowelEndings.Contains(lower)) || (!upper && lower == "u");
            return joins ? ending : match.Value;
        }), match =>
        {
            var onset = match.Groups["onset"].Value;
            var vowel = match.Groups["vowel"].Value;
            // "Phương án B ở trên": a single capital letter followed by a lower-case word is a label, not half of a syllable.
            if (onset.Length == 1 && char.IsUpper(onset[0]) && char.IsLower(vowel[0])) return match.Value;
            return onset + vowel + match.Groups["tail"].Value;
        });

    /// <summary>"6. DEMO6. DEMO6. DEMO" → "6. DEMO". Only text with letters, so "2020" stays a year.</summary>
    internal static string CollapseRepeat(string line)
    {
        var match = RepeatedLine().Match(line);
        return match.Success && IsRepeatableUnit(match.Groups["unit"].Value) ? match.Groups["unit"].Value.Trim() : line;
    }

    // Text repeats by design; a figure ("60,4%60,4%60,4%", "3.23 / 53.23 / 5") repeats when a stat card draws its number in layers.
    // A bare number such as "2020" is never collapsed: it needs a percent sign or a slash to count as a figure.
    private static bool IsRepeatableUnit(string unit) =>
        unit.Any(char.IsLetter) || (unit.Any(char.IsDigit) && unit.IndexOfAny(['%', '/']) >= 0);

    /// <summary>Joins visual lines back into sentences: a lower-case continuation, a line after a comma, or capitals.</summary>
    internal static IEnumerable<string> JoinBrokenLines(IReadOnlyList<string> lines)
    {
        var current = new StringBuilder();
        foreach (var line in lines)
        {
            if (current.Length > 0 && Continues(current.ToString(), line))
            {
                current.Append(' ').Append(line);
                continue;
            }
            if (current.Length > 0) yield return current.ToString();
            current.Clear().Append(line);
        }
        if (current.Length > 0) yield return current.ToString();
    }

    private static bool Continues(string previous, string next)
    {
        if (previous[^1] is '.' or '!' or '?' || ListItem().IsMatch(next)) return false;
        if (char.IsLower(next[0]) || next[0] == '&' || EndsOpen(previous)) return true;
        if (previous[^1] is ',' or '-' or '–') return true;
        // A paragraph written in capitals (common on slides) wraps onto lines that also start in capitals.
        return previous.Length >= CapitalParagraphLine && IsCapitals(previous) && IsCapitals(next);
    }

    private static bool IsCapitals(string text)
    {
        var letters = text.Where(char.IsLetter).ToArray();
        return letters.Length >= 3 && letters.All(char.IsUpper);
    }

    // The vowel is looked behind, not consumed, so "THEO ĐÂ Y" can match at "Â Y" after "O ĐÂ" was considered.
    [GeneratedRegex(@"(?<=(?<vowel>\p{L}))\s{1,3}(?<ending>\p{L}{1,2})(?=$|[\s.,;:!?)\]”""])")]
    private static partial Regex SplitSyllable();

    // "gi á trị" for "giá trị": a lone accented vowel right after a bare consonant cluster. After a word with a vowel it is
    // an ordinary word ("ở"), so only consonant-only tokens qualify.
    // The vowel may be followed by the rest of the word ("Qu ản", "Đ ỘNG": the ending was already joined), but a unit after a
    // number ("5 m ở ngoài") is not an onset.
    [GeneratedRegex(@"(?<![\p{L}])(?<!\d )(?<onset>gi|qu|ngh|ng|nh|th|tr|ch|kh|ph|gh|[bcdđghklmnprstvx]) (?<vowel>[àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ])(?<tail>\p{L}{0,3})(?![\p{L}])", RegexOptions.IgnoreCase)]
    private static partial Regex LoneVowel();

    [GeneratedRegex(@"^(?<unit>.{3,}?)\k<unit>+$")]
    private static partial Regex RepeatedLine();

    [GeneratedRegex(@"^(\d{1,2}[.)]\s*\S|[-•*–]\s)")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
