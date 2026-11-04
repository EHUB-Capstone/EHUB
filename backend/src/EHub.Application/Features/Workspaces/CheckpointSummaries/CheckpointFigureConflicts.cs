using System.Globalization;
using System.Text.RegularExpressions;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Finds figures a file states more than once with different values, without the model: a pitch deck that assumes a
/// 15% conversion rate on one slide and 30% on another, or splits its budget differently in two tables. Reviewers want
/// exactly these to ask about, and a 2B model does not notice them. Only a few common measures are recognised, and a
/// percentage must sit next to the word that names it, so a sentence mentioning a margin elsewhere is not misread.
/// </summary>
internal static partial class CheckpointFigureConflicts
{
    private const int MaximumConflicts = 5;
    // "8% monthly churn" names the churn; "D30 ≥15% … ask about churn" is another figure that happens to be nearby.
    private const int KeywordReach = 15;
    private const int MinimumLabelWord = 4;

    private static readonly (string Subject, Regex Keyword)[] Rates =
    [
        ("Conversion rate", new Regex(@"conversion|convert|upgrade|chuyển đổi|nâng cấp", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        ("Gross margin", new Regex(@"gross margin|margin|biên lợi nhuận", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        ("Churn rate", new Regex(@"churn|tỷ lệ rời bỏ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)),
        ("Equity offered", new Regex(@"equity|cổ phần", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
    ];

    // Words of a budget line that say nothing about which budget item it is.
    private static readonly HashSet<string> GenericWords = new(StringComparer.Ordinal)
    {
        "million", "billion", "triệu", "nghìn", "budget", "allocation", "category", "total", "tổng", "ngân", "sách"
    };

    public static IReadOnlyList<WorkspaceFigureConflict> Find(string text)
    {
        var sentences = CheckpointSummaryText.Sentences(text);
        var conflicts = new List<WorkspaceFigureConflict>();

        foreach (var (subject, keyword) in Rates)
        {
            var values = new List<(double Value, string Quote)>();
            foreach (var sentence in sentences)
            {
                var near = keyword.Matches(sentence)
                    .SelectMany(match => Percentages().Matches(sentence)
                        .Select(percent => (Percent: percent, Distance: Distance(match, percent))))
                    .Where(item => item.Distance <= KeywordReach)
                    .OrderBy(item => item.Distance)
                    .FirstOrDefault();
                if (near.Percent is not null) values.Add((Parse(near.Percent), sentence));
            }
            AddIfDifferent(conflicts, subject, values);
        }

        // Budget lines: a percentage and an amount of money, without a year or phase (which legitimately differ).
        var budget = sentences
            .Where(sentence => Percentages().IsMatch(sentence) && Money().IsMatch(sentence) && !Period().IsMatch(sentence))
            .Select(sentence => (Sentence: sentence, Value: Parse(Percentages().Match(sentence)), Words: LabelWords(sentence)))
            .Where(line => line.Words.Count > 0)
            .ToArray();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in budget)
        {
            var differing = budget.Where(other => other.Value != line.Value && other.Words.Overlaps(line.Words)).ToArray();
            if (differing.Length == 0) continue;
            var label = line.Words.Intersect(differing[0].Words).OrderBy(word => word, StringComparer.Ordinal).First();
            if (!reported.Add(label)) continue;
            AddIfDifferent(conflicts, $"Budget share: {LabelText(line.Sentence)}",
                new[] { (line.Value, line.Sentence) }.Concat(differing.Select(other => (other.Value, other.Sentence))).ToList());
        }

        return conflicts.Take(MaximumConflicts).ToArray();
    }

    /// <summary>
    /// Adds the other values to a summary sentence that states one side of a conflict, so a key point such as
    /// "tỷ lệ chuyển đổi 30%" does not read as settled while the file also says 15%. The sentence must name the measure
    /// (its keyword, or a word of the budget item) and contain exactly one of the conflicting values.
    /// </summary>
    public static string Annotate(string sentence, IReadOnlyCollection<WorkspaceFigureConflict> conflicts)
    {
        var stated = Percentages().Matches(sentence).Select(Parse).ToHashSet();
        if (stated.Count == 0) return sentence;
        foreach (var conflict in conflicts)
        {
            if (!NamesMeasure(sentence, conflict.Subject)) continue;
            var values = conflict.Values.Select(item => (Text: item.Value, Number: Parse(Percentages().Match(item.Value)))).ToArray();
            var present = values.Where(item => stated.Contains(item.Number)).ToArray();
            if (present.Length != 1) continue;
            var others = values.Where(item => item.Number != present[0].Number).Select(item => item.Text);
            return $"{sentence.TrimEnd('.', ' ')} (tài liệu cũng ghi {string.Join(" / ", others)}).";
        }
        return sentence;
    }

    private static bool NamesMeasure(string sentence, string subject)
    {
        var rate = Rates.FirstOrDefault(item => item.Subject == subject);
        if (rate.Keyword is not null) return rate.Keyword.IsMatch(sentence);
        var label = LabelWords(subject.Replace("Budget share:", string.Empty, StringComparison.Ordinal));
        return LabelWords(sentence).Overlaps(label);
    }

    private static void AddIfDifferent(List<WorkspaceFigureConflict> conflicts, string subject, List<(double Value, string Quote)> values)
    {
        var distinct = values.GroupBy(item => item.Value).Select(group => group.First()).ToArray();
        if (distinct.Length < 2) return;
        conflicts.Add(new WorkspaceFigureConflict
        {
            Subject = subject,
            Values = distinct.Select(item => new WorkspaceFigureValue
            {
                Value = item.Value.ToString("0.##", CultureInfo.InvariantCulture) + "%",
                Quote = CheckpointSummaryText.Clip(item.Quote, CheckpointSummaryLimits.MaxEvidenceCharacters)
            }).ToArray()
        });
    }

    private static int Distance(Match keyword, Match percent) =>
        percent.Index >= keyword.Index + keyword.Length ? percent.Index - (keyword.Index + keyword.Length)
        : keyword.Index >= percent.Index + percent.Length ? keyword.Index - (percent.Index + percent.Length)
        : 0;

    private static double Parse(Match percent) =>
        double.Parse(percent.Groups["number"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>"Team Expansion 20% 100 Million VND" → "Team Expansion": the line without its figures.</summary>
    private static string LabelText(string sentence)
    {
        var label = AmountOrPercent().Replace(sentence, " ");
        label = Whitespace().Replace(label, " ").Trim(' ', ':', '(', ')', '-', '–', '.');
        return string.IsNullOrEmpty(label) ? "item" : label;
    }

    private static HashSet<string> LabelWords(string sentence) =>
        LetterWords().Matches(sentence.ToLower(CultureInfo.InvariantCulture))
            .Select(match => match.Value)
            .Where(word => word.Length >= MinimumLabelWord && !GenericWords.Contains(word))
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"(?<number>\d{1,3}(?:[.,]\d+)?)\s?%")]
    private static partial Regex Percentages();

    [GeneratedRegex(@"\d\s?(?:million|billion|triệu|tỷ|nghìn|k\b|vnd|đồng|usd|\$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Money();

    [GeneratedRegex(@"\b(?:year|năm|quý|quarter|month|tháng|phase|giai đoạn|q[1-4])\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Period();

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex LetterWords();

    [GeneratedRegex(@"\(?\s*\d[\d.,]*\s?(?:%|million|billion|triệu|tỷ|nghìn|k\b)?\s*(?:vnd|đồng|usd)?\s*\)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountOrPercent();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
