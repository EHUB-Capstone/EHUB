using System.Globalization;
using System.Text.RegularExpressions;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Finds figures that two files of one checkpoint state differently, without the model: a sample of 51 in the slides and
/// 52 in the survey report, or "84,3% spend over 10 minutes" next to "82,7% … to find an old document". Each file keeps
/// the few sentences that carry a percentage or a sample size when it is read; the files are compared when the screen asks.
/// It points at a difference to ask about, never at which file is right: the two may describe different things.
/// </summary>
internal static partial class CheckpointCrossFileFigures
{
    private const int MaximumFiguresPerFile = 40;
    private const int MaximumConflicts = 5;
    private const int MinimumSentenceLength = 20;
    private const int MaximumSentenceLength = 300;

    // Two sentences are "the same statement" when they share this many words in a row (digits break the run).
    private const int SharedRunWords = 4;
    private const int MinimumRunCharacters = 10;

    // How many words a figure may sit from the words it describes, in a line that lists several figures.
    private const int PercentReach = 3;

    /// <summary>The sentences of a file that state a sample size or a percentage, sample sizes first, quoted as written.</summary>
    public static IReadOnlyList<string> Collect(string fileText)
    {
        var contents = CheckpointSummaryText.ContentsLines(CheckpointSummaryText.Sentences(fileText));
        var candidates = CheckpointSummaryText.Sentences(fileText)
            .Where((sentence, position) => !contents[position] && !sentence.EndsWith('?') &&
                sentence.Length is >= MinimumSentenceLength and <= MaximumSentenceLength)
            .ToArray();
        return candidates.Where(sentence => SampleSize(sentence) is not null)
            .Concat(candidates.Where(sentence => SampleSize(sentence) is null && Percentages().IsMatch(sentence)))
            .Take(MaximumFiguresPerFile)
            .ToArray();
    }

    public static IReadOnlyList<WorkspaceFigureConflict> Compare(
        IReadOnlyList<(string FileName, IReadOnlyCollection<string> Figures)> files)
    {
        if (files.Count < 2) return [];
        var conflicts = new List<WorkspaceFigureConflict>();
        AddSampleSizes(conflicts, files);
        AddPercentages(conflicts, files);
        return conflicts.Take(MaximumConflicts).ToArray();
    }

    private static void AddSampleSizes(
        List<WorkspaceFigureConflict> conflicts, IReadOnlyList<(string FileName, IReadOnlyCollection<string> Figures)> files)
    {
        // The first sample size each file states; a file that gives several (people and companies) is judged by its first.
        var stated = files
            .Select(file => (file.FileName, Sample: file.Figures.Select(sentence => (Sentence: sentence, Size: SampleSize(sentence)))
                .FirstOrDefault(item => item.Size is not null)))
            .Where(item => item.Sample.Size is not null)
            .ToArray();
        var distinct = stated.GroupBy(item => item.Sample.Size!.Value).Select(group => group.First()).ToArray();
        if (distinct.Length < 2) return;
        conflicts.Add(new WorkspaceFigureConflict
        {
            Subject = "Survey sample size",
            Values = distinct.Select(item => new WorkspaceFigureValue
            {
                Value = item.Sample.Size!.Value.ToString(CultureInfo.InvariantCulture),
                Quote = Quote(item.Sample.Sentence),
                Source = item.FileName
            }).ToArray()
        });
    }

    private static void AddPercentages(
        List<WorkspaceFigureConflict> conflicts, IReadOnlyList<(string FileName, IReadOnlyCollection<string> Figures)> files)
    {
        var prepared = files.Select(file => (file.FileName, Lines: file.Figures
            .Select(sentence => (Sentence: sentence, Anchors: Anchor(sentence)))
            .Where(line => line.Anchors.Count > 0)
            .ToArray())).ToArray();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        for (var first = 0; first < prepared.Length; first++)
        for (var second = first + 1; second < prepared.Length; second++)
            foreach (var a in prepared[first].Lines)
            foreach (var b in prepared[second].Lines)
                foreach (var left in a.Anchors)
                {
                    var right = b.Anchors.FirstOrDefault(item => item.Gram == left.Gram);
                    if (right is null || left.Percent == right.Percent) continue;
                    // The same pair of values between the same two files is one finding, however many sentences repeat it.
                    var key = $"{first}|{second}|{left.Percent}|{right.Percent}";
                    if (!reported.Add(key)) continue;
                    conflicts.Add(new WorkspaceFigureConflict
                    {
                        Subject = $"Similar statement: “{left.Gram}”",
                        Values =
                        [
                            Value(a.Sentence, left.Text, prepared[first].FileName),
                            Value(b.Sentence, right.Text, prepared[second].FileName)
                        ]
                    });
                }
    }

    private static WorkspaceFigureValue Value(string sentence, string value, string fileName) => new()
    {
        Value = value,
        Quote = Quote(sentence),
        Source = fileName
    };

    private static string Quote(string sentence) => CheckpointSummaryText.Clip(sentence, CheckpointSummaryLimits.MaxEvidenceCharacters);

    /// <summary>The number of respondents a sentence reports, when it also talks about a survey or a sample.</summary>
    internal static int? SampleSize(string sentence)
    {
        if (!SurveyWords().IsMatch(sentence)) return null;
        var match = Sample().Match(sentence);
        if (!match.Success) return null;
        var digits = match.Groups["n1"].Success ? match.Groups["n1"].Value : match.Groups["n2"].Value;
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var size) ? size : null;
    }

    private sealed record Anchored(string Gram, double Percent, string Text);

    /// <summary>
    /// Each run of <see cref="SharedRunWords"/> words without a figure, with the percentage it is about: the only one of the
    /// sentence, or the nearest of several. A line that lists many percentages ("52 Doanh nghiệp 98% … 82,7% Mất nhiều thời
    /// gian") says which figure belongs to which words only by position, so a run far from every percentage is left out.
    /// </summary>
    private static List<Anchored> Anchor(string sentence)
    {
        var percents = Percentages().Matches(sentence);
        var words = CheckpointSummaryText.Normalize(sentence).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var positions = Enumerable.Range(0, words.Length).Where(index => words[index].Contains('%')).ToArray();
        // The k-th "%" of the words is the k-th percentage of the sentence; when they disagree the position is unknown.
        if (percents.Count == 0 || (percents.Count > 1 && positions.Length != percents.Count)) return [];
        var anchors = new List<Anchored>();
        for (var i = 0; i + SharedRunWords <= words.Length; i++)
        {
            var run = words.Skip(i).Take(SharedRunWords).ToArray();
            if (run.Any(word => word.Any(char.IsDigit) || word.Contains('%'))) continue;
            if (run.Sum(word => word.Length) < MinimumRunCharacters) continue;
            var which = 0;
            if (percents.Count > 1)
            {
                var distances = positions.Select(position => position < i ? i - position : position - (i + SharedRunWords - 1)).ToArray();
                which = Array.IndexOf(distances, distances.Min());
                if (distances[which] > PercentReach) continue;
            }
            anchors.Add(new Anchored(string.Join(' ', run), Parse(percents[which]), percents[which].Value.Trim()));
        }
        return anchors;
    }

    private static double Parse(Match percent) =>
        double.Parse(percent.Groups["number"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(?<number>\d{1,3}(?:[.,]\d+)?)\s?%")]
    private static partial Regex Percentages();

    [GeneratedRegex(@"\b(?:khảo sát|mẫu|sample|survey|respondents?|tham gia|trả lời|phản hồi|nghiên cứu|participants?|responses?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SurveyWords();

    [GeneratedRegex(@"\bN\s*=\s*(?<n1>\d{1,6})\b|(?<![\d.,%])(?<n2>\d{1,6})\+?\s*(?:doanh nghiệp|đơn vị|người trả lời|người tham gia|người được khảo sát|phản hồi|phiếu|nhân sự|respondents?|participants?|responses?|companies|businesses)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Sample();
}
