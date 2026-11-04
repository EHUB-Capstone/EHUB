using System.Text;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Finds, without the model, the sentence of a file that best shows each checklist item. A sentence counts only through
/// its affirmative part (so "Nhóm chưa có kế hoạch gọi vốn" is not evidence of a funding plan), and it matches an item
/// by shared two-word phrases (accents ignored) or by a shared startup topic from the mentor model's alias catalog.
/// The result is a quote from the file, never a judgement of whether the item is done well.
/// </summary>
internal static class CheckpointSummaryEvidence
{
    public const string Related = "Related";
    public const string NotFound = "NotFound";

    /// <summary>The criterion is judged live; no file can show it.</summary>
    public const string Live = "Live";

    /// <summary>The criterion is judged on the product or demo, and the files do not mention it.</summary>
    public const string Product = "Product";

    /// <summary>The criterion is judged on the whole file and no passage stands for it.</summary>
    public const string Overall = "Overall";

    private const int HighlightsPerFile = 3;

    // How far a matching title lends its weight: slide PDFs put a title before or after its content.
    private const int TitleReach = 3;
    private const int ShortLineWords = 8;
    private const int IntroLineWords = 14;
    private const int MinimumNameWord = 5;
    private const int ContextLines = 3;

    // A sentence already quoted for another criterion keeps this share of its score, so a close second wins instead.
    private const double RepeatedQuoteShare = 0.6;

    // A sentence about method ("Thang đo chuẩn hóa: Likert 5 mức") keeps this share for criteria that are not about research.
    private const double MethodologyShare = 0.4;
    private const int ContextCharacters = 220;

    // Concepts that ask for what the team will do; a recommendation is then a fair answer.
    private static readonly string[] ActionConcepts = ["Solution / value", "Roadmap", "Prioritization", "Core features", "Launch"];

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "va", "hoac", "cua", "co", "cac", "da", "sau", "khi", "the", "and", "or", "of", "for", "to", "in", "a", "an", "is", "are",
        "with", "by", "on", "its", "their"
    };

    public static WorkspaceFilePreview Preview(
        string fileText, IReadOnlyList<ChecklistItem> checklist, CheckpointSummaryTopics topics, bool littleText = false,
        int ignoredInstructions = 0) => new()
    {
        Highlights = CheckpointSummaryText.Highlights(fileText, ChecklistTerms(checklist), HighlightsPerFile)
            .Select(text => CheckpointSummaryText.Clip(text, CheckpointSummaryLimits.MaxEvidenceCharacters))
            .ToArray(),
        Requirements = Find(fileText, checklist, topics),
        Conflicts = CheckpointFigureConflicts.Find(fileText),
        StatusItems = CheckpointStatusLines.Find(fileText),
        Figures = CheckpointCrossFileFigures.Collect(fileText),
        IgnoredInstructions = ignoredInstructions,
        LittleText = littleText
    };

    /// <summary>
    /// Titles with their descriptions: a rubric title ("Problem Clarity") rarely appears in a file, its description's
    /// phrases do. Criteria judged live (a presentation, a Q&amp;A) say nothing about what the documents contain, so they
    /// are left out; they would only pull sentences about "presentation" and "answers" into the prompt.
    /// </summary>
    public static string[] ChecklistTerms(IReadOnlyList<ChecklistItem> checklist) =>
        checklist.Where(item => item.Assessment != CheckpointCriterionProfile.Live)
            .Select(item => string.IsNullOrWhiteSpace(item.Description) ? item.Title : item.Title + ". " + item.Description).ToArray();

    public static IReadOnlyList<WorkspaceFileRequirementFinding> Find(
        string fileText, IReadOnlyList<ChecklistItem> checklist, CheckpointSummaryTopics topics)
    {
        var sentences = CheckpointSummaryText.Sentences(fileText);
        // Each sentence is prepared once: its affirmative words and the topics it names.
        var prepared = sentences.Select(sentence =>
        {
            var positive = MentorEvidenceText.Positive(sentence);
            var title = CheckpointSummaryText.IsTitle(sentence);
            return (Pairs: Pairs(positive), Topics: topics.Find(sentence), Title: title, Short: !title && IsShortLine(sentence),
                Words: Words(sentence));
        }).ToArray();

        var titles = prepared.Select(line => line.Title).ToArray();
        // Lines that are never evidence for any criterion: questions (an item of a survey, not what the team found), a
        // chart's labels read as one line, and runs of capitals (report titles, glued labels).
        var neverQuoted = sentences.Select((sentence, position) => sentence.EndsWith('?') || CheckpointSummaryText.IsLabelStrip(sentence) ||
            CheckpointSummaryText.IsInterfaceText(sentence) ||
            // "Nhóm sẽ hoàn thiện lộ trình và các phần còn lại sau" says the roadmap is not there yet.
            CheckpointSummaryText.IsDeferral(MentorRankingFeatures.Normalize(sentence)) ||
            (!prepared[position].Title && !prepared[position].Short && CheckpointSummaryText.IsMostlyCapitals(sentence))).ToArray();
        // A heading with no content under it (only table cells, or the next numbered topic) shows the file covers a topic
        // but has nothing to quote.
        var hollow = Enumerable.Range(0, sentences.Count).Select(i => prepared[i].Short && IsHollow(sentences, titles, i)).ToArray();

        // Accent-free affirmative text of every sentence, for the dictionary of expected things (roadmap, persona...).
        var normalised = sentences.Select(sentence => MentorRankingFeatures.Normalize(MentorEvidenceText.Positive(sentence))).ToArray();
        // The agenda after "Table of Contents" names the sections; it is not evidence about them.
        var contents = CheckpointSummaryText.ContentsLines(sentences);

        // Sentences already quoted for an earlier criterion. Three criteria of one rubric (problem, research, fit) often
        // match the same intro sentence of a survey; each should show the sentence that is most about it.
        var quoted = new HashSet<int>();
        var usedEvidence = new HashSet<string>(StringComparer.Ordinal);
        return checklist.Select(item =>
        {
            // Presentation, Q&A and teamwork are judged live: no file can show them, so they are not searched for and
            // never reported as missing.
            if (item.Assessment == CheckpointCriterionProfile.Live)
                return new WorkspaceFileRequirementFinding
                {
                    Index = item.Index, Kind = item.Kind, Title = item.Title, Finding = Live, Weight = item.Weight,
                    Assessment = item.Assessment
                };
            var terms = item.Title + ". " + item.Description;
            var itemPairs = Pairs(MentorRankingFeatures.Normalize(terms));
            var itemTopics = topics.Find(terms);
            // The criterion's own name ("Solution Fit") often appears only as a slide title ("SOLUTIONS"). All of its
            // longer words must be there: "BUSINESS WORKFLOW" is not about the business model.
            var nameWords = Words(item.Title).Where(word => word.Length >= MinimumNameWord).ToHashSet(StringComparer.Ordinal);
            // Breaks a tie between the lines before and after a matching title (slides put titles on either side).
            var itemWords = Words(terms).Where(word => word.Length >= MinimumNameWord).ToHashSet(StringComparer.Ordinal);
            // The things the criterion expects (roadmap, persona...): a sentence that mentions them is evidence even when
            // it shares no phrase with the criterion's name, which is how a rubric title and a student's slide differ.
            var wanted = CheckpointCriterionCues.Wanted(item.Title, item.Excellent ?? item.Description);
            // How the data was collected or analysed is not what it found. It stays evidence for a criterion about research.
            var wantsResearch = wanted.Contains("Research");
            // A criterion about the problem wants the problem, not what the team advises doing about it; one that does not
            // ask for competitors is not shown by a row of the competitor table.
            var wantsProblemOnly = wanted.Contains("Problem") && !wanted.Any(ActionConcepts.Contains);
            var wantsCompetitors = wanted.Contains("Competitors");
            // Lines that cannot be evidence for this criterion at all: they neither score nor count as a mention. A weak quote
            // would mislead more than no quote.
            var hidden = Enumerable.Range(0, sentences.Count).Select(position => contents[position] || neverQuoted[position] ||
                (wantsProblemOnly && CheckpointSummaryText.IsRecommendation(normalised[position])) ||
                (!wantsCompetitors && CheckpointCriterionCues.Mentions("Competitors", normalised[position]))).ToArray();
            var own = prepared.Select((sentence, position) => hidden[position] ? 0 :
                (2.0 * sentence.Pairs.Count(itemPairs.Contains) + sentence.Topics.Count(itemTopics.Contains) +
                ((sentence.Title || sentence.Short) && nameWords.Count > 0 && nameWords.IsSubsetOf(sentence.Words) ? 1 : 0) +
                (sentence.Title ? 0 : 2.0 * CheckpointCriterionCues.CountMentions(wanted, normalised[position]))) *
                (!wantsResearch && CheckpointSummaryText.IsMethodology(sentences[position]) ? MethodologyShare : 1.0)).ToArray();

            // A title is never quoted: it says only what the slide is about. A matching slide title lends half its weight
            // to the lines around it, nearer lines more (they say what the title promises without repeating its words).
            // A matching short heading ("Phân khúc và người tham gia khảo sát") is quoted itself, with the lines after it;
            // it lends only to neighbours that match on their own, or a line about something else beside it would win.
            var total = new double[sentences.Count];
            for (var i = 0; i < sentences.Count; i++)
            {
                if (!prepared[i].Title) total[i] += own[i];
                if ((prepared[i].Title || prepared[i].Short) && own[i] > 0)
                    for (var distance = 1; distance <= TitleReach; distance++)
                        foreach (var j in new[] { i - distance, i + distance })
                            if (j >= 0 && j < sentences.Count && !prepared[j].Title && (prepared[i].Title || own[j] > 0))
                                total[j] += own[i] / (2.0 * distance);
            }
            var best = Enumerable.Range(0, sentences.Count)
                .Where(i => !prepared[i].Title && total[i] > 0 && !hollow[i] && !hidden[i])
                .OrderByDescending(i => total[i] * (quoted.Contains(i) ? RepeatedQuoteShare : 1.0))
                .ThenBy(i => prepared[i].Short)
                .ThenByDescending(i => own[i]).ThenByDescending(i => prepared[i].Words.Count(itemWords.Contains)).ThenBy(i => i)
                .DefaultIfEmpty(-1).First();
            // A sentence already quoted for an earlier criterion is not quoted again when nothing else matches: it would
            // suggest the file has a second passage on a different subject.
            if (best >= 0 && quoted.Contains(best)) best = -1;
            if (best >= 0) quoted.Add(best);
            // How the data was collected is not a mention of the problem; it counts only for a criterion about research.
            var cueHidden = hidden.Select((line, position) => line || (!wantsResearch && CheckpointSummaryText.IsMethodology(sentences[position]))).ToArray();
            var cues = CheckpointCriterionCues.Find(item.Title, item.Excellent ?? item.Description, sentences, normalised, cueHidden, hollow);
            // A mentioned thing is also related content: "Not found" next to "Mentioned: Target customer" contradicts itself.
            // Its sentence is quoted when there is one; a mention under a heading with nothing to quote is reported without.
            var mentioned = best < 0 && cues.Any(cue => cue.Found);
            // A quote another criterion already shows is not shown again as its evidence.
            var cueEvidence = best >= 0 ? null
                : cues.FirstOrDefault(cue => cue.Found && !string.IsNullOrWhiteSpace(cue.Evidence) && !usedEvidence.Contains(EvidenceKey(cue.Evidence!)))?.Evidence;
            var evidence = best >= 0
                ? CheckpointSummaryText.Clip(WithContext(sentences, titles, best, prepared[best].Short), CheckpointSummaryLimits.MaxEvidenceCharacters)
                : cueEvidence;
            if (evidence is not null) usedEvidence.Add(EvidenceKey(evidence));
            return new WorkspaceFileRequirementFinding
            {
                Index = item.Index,
                Kind = item.Kind,
                Title = item.Title,
                // A product criterion that the files do not mention is judged on the product itself.
                Finding = best >= 0 || mentioned ? Related : MissingFinding(item.Assessment),
                Evidence = evidence,
                Weight = item.Weight,
                Assessment = item.Assessment,
                Cues = cues
            };
        }).ToArray();
    }

    /// <summary>
    /// A short line ("Students &amp; Working Professionals") names a topic but does not show it, so the lines after it
    /// are added until the quote says something: "Students &amp; Working Professionals … Frequently copy content from AI…".
    /// </summary>
    private static string WithContext(IReadOnlyList<string> sentences, bool[] titles, int best, bool shortLine)
    {
        if (!shortLine) return sentences[best];
        var quote = new StringBuilder(sentences[best]);
        for (var next = best + 1; next < sentences.Count && next <= best + ContextLines && !titles[next]; next++)
        {
            // A table cell or a stray letter after a heading ("… Nhu cầu/JTB … D") adds nothing to read.
            if (quote.Length + sentences[next].Length > ContextCharacters || CheckpointSummaryText.IsFragment(sentences[next]) ||
                CheckpointSummaryText.IsNumberedLabel(sentences[next])) break;
            quote.Append(" … ").Append(sentences[next]);
        }
        return quote.ToString();
    }

    /// <summary>The checklist items the quick view found in the file; only these are shown to the model as an outline.</summary>
    public static IReadOnlyList<ChecklistItem> Covered(
        IReadOnlyList<ChecklistItem> checklist, IReadOnlyCollection<WorkspaceFileRequirementFinding> findings) =>
        checklist.Where(item => findings.Any(found => found.Index == item.Index && found.Finding == Related)).ToArray();

    /// <summary>What to report when nothing in the files matches: a product or a whole-file criterion is not "missing".</summary>
    public static string MissingFinding(string assessment) => assessment switch
    {
        CheckpointCriterionProfile.Product => Product,
        CheckpointCriterionProfile.Overall => Overall,
        _ => NotFound
    };

    private const int EvidenceKeyCharacters = 60;

    /// <summary>The start of a quote, so a quote clipped at two lengths is still recognised as the same one.</summary>
    private static string EvidenceKey(string evidence) =>
        CheckpointSummaryText.Normalize(evidence) is { Length: > EvidenceKeyCharacters } text ? text[..EvidenceKeyCharacters] : CheckpointSummaryText.Normalize(evidence);

    /// <summary>A heading that no line with content follows: the next line is a table cell, a title, or missing.</summary>
    private static bool IsHollow(IReadOnlyList<string> sentences, bool[] titles, int position) =>
        position + 1 >= sentences.Count || titles[position + 1] || CheckpointSummaryText.IsThin(sentences[position + 1]) ||
        (CheckpointSummaryText.IsNumberedLabel(sentences[position]) && CheckpointSummaryText.IsNumberedLabel(sentences[position + 1]));

    /// <summary>A short line without a full stop: a heading in a report, a bullet on a slide.</summary>
    private static bool IsShortLine(string sentence) =>
        (CheckpointSummaryText.IsHeading(sentence) && sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= ShortLineWords) ||
        CheckpointSummaryText.IsNumberedLabel(sentence) ||
        // "Interview Kiểm chứng pain point, buying process, 12–15 người:" introduces what follows; the lines after it are the content.
        (sentence.TrimEnd().EndsWith(':') && sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= IntroLineWords);

    /// <summary>Accent-free words with a plural "s" removed, so "SOLUTIONS" matches "Solution".</summary>
    private static HashSet<string> Words(string text) =>
        MentorRankingFeatures.Normalize(text)
            .Split([' ', ',', '.', ':', ';', '(', ')', '-', '/', '&', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length > MinimumNameWord && word.EndsWith('s') ? word[..^1] : word)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Two-word phrases of already normalised (lower-case, accent-free) text, within one clause: "statement, customer"
    /// is not a phrase.
    /// </summary>
    private static HashSet<string> Pairs(string normalized)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var clause in normalized.Split([',', '.', ':', ';', '(', ')', '"', '|', '→'], StringSplitOptions.RemoveEmptyEntries))
        {
            var words = clause.Split([' ', '-', '/', '\'', '&'], StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i + 1 < words.Length; i++)
                if (!StopWords.Contains(words[i]) && !StopWords.Contains(words[i + 1])) result.Add($"{words[i]} {words[i + 1]}");
        }
        return result;
    }
}
