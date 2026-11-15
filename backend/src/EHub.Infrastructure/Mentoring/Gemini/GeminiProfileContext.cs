using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EHub.Infrastructure.Mentoring.Gemini;

/// <summary>Selects bounded source passages; all semantic scores still come from the configured model.</summary>
internal static class GeminiProfileContext
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
        { "the", "and", "with", "for", "from", "this", "that", "project", "startup", "mentor", "can", "have", "has",
          "cua", "cho", "cac", "nhung", "voi", "trong", "duoc", "mot", "nhom", "can", "kinh", "nghiem", "du", "an" };

    internal static string Select(string source, IReadOnlyCollection<string> projects, int maximum)
    {
        if (source.Length <= maximum) return source;
        var passages = Regex.Split(source, @";\s*|(?<=[.!?])\s+", RegexOptions.CultureInvariant, RegexTimeout)
            .Select(text => text.Trim()).Where(text => text.Length > 0).ToArray();
        var queryTerms = projects.Select(Terms).ToArray();
        var passageTerms = passages.Select(Terms).ToArray();
        var overlap = passageTerms.Select(terms => queryTerms.Select(query => terms.Count(query.Contains)).ToArray()).ToArray();
        var selected = new HashSet<int>();
        var length = 0;
        void Add(int index)
        {
            if (selected.Contains(index)) return;
            var added = passages[index].Length + (selected.Count > 0 ? 2 : 0);
            if (length + added <= maximum) { selected.Add(index); length += added; }
        }
        // Cover individual project needs, so a frequently repeated topic cannot exclude all other topics in the batch.
        for (var project = 0; project < queryTerms.Length; project++)
        {
            var index = Enumerable.Range(0, passages.Length).Where(index => overlap[index][project] > 0)
                .OrderByDescending(index => overlap[index][project]).ThenBy(index => passages[index].Length).ThenBy(index => index)
                .FirstOrDefault(-1);
            if (index >= 0) Add(index);
        }
        foreach (var index in Enumerable.Range(0, passages.Length)
                     .OrderByDescending(index => overlap[index].Sum()).ThenBy(index => index)) Add(index);
        // Never create a new substring by dropping a negation from the start of a long clause.
        if (selected.Count == 0)
        {
            var limit = Math.Min(source.Length, maximum);
            if (limit > 0 && char.IsHighSurrogate(source[limit - 1])) limit--;
            return source[..limit];
        }
        return string.Join("; ", selected.Order().Select(index => passages[index]));
    }

    internal static IEnumerable<string> Prioritize(string[] passages, IReadOnlyCollection<string> projects)
    {
        var queries = projects.Select(Terms).ToArray();
        var terms = passages.Select(Terms).ToArray();
        var overlaps = terms.Select(words => queries.Select(query => words.Count(query.Contains)).ToArray()).ToArray();
        var seen = new HashSet<int>();
        for (var project = 0; project < queries.Length; project++)
        {
            var best = Enumerable.Range(0, passages.Length).Where(index => overlaps[index][project] > 0)
                .OrderByDescending(index => overlaps[index][project]).ThenBy(index => passages[index].Length).ThenBy(index => index)
                .FirstOrDefault(-1);
            if (best >= 0 && seen.Add(best)) yield return passages[best];
        }
        foreach (var index in Enumerable.Range(0, passages.Length).OrderByDescending(index => overlaps[index].Sum()).ThenBy(index => index))
            if (seen.Add(index)) yield return passages[index];
    }

    private static HashSet<string> Terms(string text)
    {
        var folded = new StringBuilder();
        foreach (var character in text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) folded.Append(character);
        return Regex.Matches(folded.ToString(), @"[\p{L}\p{N}+#.]{2,}", RegexOptions.CultureInvariant, RegexTimeout)
            .Select(match => match.Value.Trim('.')).Where(term => term.Length >= 2 && !StopWords.Contains(term)).ToHashSet(StringComparer.Ordinal);
    }
}
