using System.Text.RegularExpressions;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Turns the Excellent level of a rubric criterion ("Clear, realistic roadmap; well-prioritized core features linked to
/// user needs") into the things a lecturer expects to see (roadmap, prioritization, core features, user needs) and checks,
/// without the model, which of them a file mentions. It shows presence of evidence, never a grade: a file that mentions
/// a roadmap may still have a poor one.
/// Rubrics are written in English while students write in English or Vietnamese, so each concept lists its terms in
/// both languages. Terms are regular expressions over accent-free lower-case text (see <c>MentorRankingFeatures.Normalize</c>).
/// </summary>
internal static class CheckpointCriterionCues
{
    private const int MaximumCues = 6;
    private const int InformativeWords = 4;

    private sealed record Concept(string Label, Regex Terms);

    private static Concept C(string label, string terms) =>
        new(label, new Regex(@"(?<![a-z0-9])(?:" + terms + ")", RegexOptions.Compiled | RegexOptions.CultureInvariant));

    private static readonly Concept[] Concepts =
    [
        C("Roadmap", @"roadmap|milestone|lo trinh|ke hoach phat trien|q[1-4] 20\d\d"),
        C("Core features", @"core features?|key features?|main features?|tinh nang (?:chinh|cot loi|chu chot)|core functions?"),
        C("Prioritization", @"prioriti[sz]|uu tien|moscow|must have|mvp scope"),
        C("User needs", @"user needs?|customer needs?|nhu cau|user stor|target users?"),
        C("Persona", @"personas?\b|chan dung|target audience|user profile|ideal customer"),
        C("Target customer", @"target customers?|customer segments?|market segments?|segments?\b|phan khuc (?:khach|nguoi|doanh|muc|ban|uu|chinh|beachhead)|(?:khach hang|nguoi dung|doi tuong) (?:muc tieu|chinh|uu tien)|doi tuong khach hang|beachhead|icp\b|customer base|tep khach hang|tep [\d.,]+ (?:doanh nghiep|khach hang|nguoi dung|sinh vien|nha hang|cua hang|sme)"),
        C("Customer journey (CJM)", @"customer journey|user journey|cjm|journey map|hanh trinh (?:khach hang|nguoi dung)"),
        C("Marketing plan", @"marketing|content plan|channels?|chien dich|kenh (?:truyen thong|tiep can)|go to market|gtm|tiep thi"),
        C("Product / demo", @"mvp|prototype|demos?\b(?! day)|ban mau|san pham mau|working product|san pham hoat dong"),
        C("UI/UX", @"ui|ux|giao dien|wireframe|figma|mock ?up|trai nghiem nguoi dung"),
        C("Stability", @"stability|bugs?\b|on dinh|crash"),
        // Not "hạn chế" (limitation): a slide listing the product's own limits is not the customers' problem.
        C("Problem", @"problem(?![- ]solving)|van de|pain points?|noi dau|kho khan|rui ro|thach thuc|bat cap|phai cho|cho qua|(?:ton|mat) (?:nhieu )?thoi gian|bat tien|lang phi|khong co thoi gian|gap kho|rat kho|kho (?:tim|biet|truy|quan|tinh)|thieu hut"),
        C("Solution / value", @"solution|giai phap|giai quyet|khac phuc|value proposition|gia tri|loi the canh tranh|competitive advantage|tu dong (?:ghep|sap xep|hoa)|canh bao (?:do|tu dong)"),
        C("Case requirements", @"case|requirements?|yeu cau|de bai|brief"),
        C("Launch", @"launch|ra mat|go live|phat hanh|soft launch"),
        C("Feedback", @"feedback|phan hoi|survey|khao sat|nhan xet|review"),
        C("Real users", @"real users?|beta users?|early adopters?|testers?|nguoi dung (?:that|thuc)"),
        C("Sales", @"sales|sold|revenue|doanh thu|doanh so|ban hang|don hang|orders?"),
        C("Proof (bills, screenshots)", @"bills?|hoa don|invoices?|receipts?|screenshots?|anh chup|bang chung|proof"),
        C("Usage data", @"usage|active users?|dau|mau|retention|analytics?|metrics?|so lieu su dung"),
        // Not "analysis": "Market Problem Analysis" would then count a sentence about the analysis method ("Thống kê định
        // lượng… Min, Max, Trung vị") as a finding about the problem.
        C("Insights / lessons", @"insights?|lessons?|bai hoc|rut ra|learnings?|takeaways?|key findings?|phat hien chinh"),
        C("Business model", @"business model|mo hinh kinh doanh|revenue model|revenue streams?|monetiz|pricing|bang gia|freemium|subscription|thu phi|phi (?:thue bao|hoa hong|dich vu)|thue bao|nguon thu|dong doanh thu|goi (?:tra phi|cao cap|premium)|tra phi|gia (?:thue|tour|ve|ban|goi|dich vu)|goi (?:thang|nam)"),
        C("Canvas (BMC)", @"canvas|bmc"),
        C("Market size", @"tam|sam|som|market size|quy mo thi truong"),
        C("Competitors", @"competitors?|competitive|doi thu|canh tranh|alternatives?|giai phap thay the"),
        C("Research", @"research|interview|survey|questionnaire|respondents?|sample size|khao sat|bang hoi|phieu hoi|nguoi tra loi|phong van|nghien cuu|focus group|desk research"),
        C("Financials", @"financial|tai chinh|costs?|chi phi|break even|hoa von|cash flow|dong tien|unit economics|cac|ltv"),
        C("Team roles", @"roles?|vai tro|ceo|cto|phan cong|members?|thanh vien"),
        C("Innovation", @"innovat|sang tao|unique|differentiat|khac biet|usp|original")
    ];

    /// <summary>
    /// The concepts a criterion names, in the order they appear: its title first ("(Persona &amp; CJM) + Marketing Plan"),
    /// then its Excellent text. The title often lists things the level text leaves out.
    /// </summary>
    public static IReadOnlyList<string> Wanted(string? title, string? excellentText)
    {
        var text = MentorRankingFeatures.Normalize(title + ". " + excellentText);
        if (text.Length == 0) return [];
        return Concepts
            .Select(concept => (concept.Label, Match: concept.Terms.Match(text)))
            .Where(item => item.Match.Success)
            .OrderBy(item => item.Match.Index)
            .Select(item => item.Label)
            .Take(MaximumCues)
            .ToArray();
    }

    /// <summary>Checks each wanted concept against the sentences of the file (accent-free affirmative text).</summary>
    /// <param name="hidden">
    /// Lines that name things without showing them (the agenda after "Table of Contents"); they never count as a mention.
    /// </param>
    /// <param name="quoteless">
    /// Lines that show the file does cover the thing but are not worth quoting (a heading with only table cells under it):
    /// the cue is found, without evidence, unless a quotable line mentions it too.
    /// </param>
    public static IReadOnlyList<WorkspaceCriterionCue> Find(
        string? title, string? excellentText, IReadOnlyList<string> sentences, IReadOnlyList<string> normalisedSentences,
        IReadOnlyList<bool>? hidden = null, IReadOnlyList<bool>? quoteless = null)
    {
        var cues = new List<WorkspaceCriterionCue>();
        foreach (var label in Wanted(title, excellentText))
        {
            var concept = Concepts.First(item => item.Label == label);
            // A sentence that says something ("Milestone Q3/2026: complete MVP version 2") shows the concept better than
            // a one-word line ("Milestone"); fall back to the short line when there is nothing longer.
            var hit = -1;
            var titleHit = -1;
            var informative = false;
            var mentionOnly = false;
            for (var i = 0; i < normalisedSentences.Count; i++)
            {
                if (hidden is not null && hidden[i]) continue;
                if (!concept.Terms.IsMatch(normalisedSentences[i])) continue;
                if (quoteless is not null && quoteless[i]) { mentionOnly = true; continue; }
                // A slide titled "TARGET AUDIENCE" shows the persona through the lines under it.
                if (CheckpointSummaryText.IsTitle(sentences[i])) { if (titleHit < 0) titleHit = i; continue; }
                if (hit < 0) hit = i;
                if (sentences[i].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= InformativeWords) { hit = i; informative = true; break; }
            }
            if (hit < 0) hit = titleHit;
            cues.Add(new WorkspaceCriterionCue
            {
                Label = label,
                Found = hit >= 0 || mentionOnly,
                Evidence = hit < 0 ? null
                    : CheckpointSummaryText.Clip(informative ? sentences[hit] : WithContext(sentences, hit), CueEvidenceCharacters)
            });
        }
        return cues;
    }

    /// <summary>A short line or a title with the (up to two) content lines that follow it, so the quote says something.</summary>
    private static string WithContext(IReadOnlyList<string> sentences, int position)
    {
        var quote = new System.Text.StringBuilder(sentences[position]);
        for (var next = position + 1; next < sentences.Count && next <= position + ContextLines && !CheckpointSummaryText.IsTitle(sentences[next]) &&
            !CheckpointSummaryText.IsFragment(sentences[next]); next++)
            quote.Append(" … ").Append(sentences[next]);
        return quote.ToString();
    }

    private const int ContextLines = 2;

    /// <summary>
    /// How strongly a sentence (accent-free, affirmative) mentions the wanted concepts: each concept counts once, and a
    /// second different term of the same concept ("khảo sát" and "bảng hỏi") counts again. A sentence describing the survey
    /// method then beats a title line that only says "research".
    /// </summary>
    public static int CountMentions(IReadOnlyList<string> wanted, string normalisedSentence) =>
        wanted.Sum(label => Math.Min(MaximumTermsPerConcept,
            Concepts.First(item => item.Label == label).Terms.Matches(normalisedSentence).Select(match => match.Value).Distinct().Count()));

    private const int MaximumTermsPerConcept = 2;

    /// <summary>Whether an accent-free sentence mentions one concept (a competitor row is not about the customers' problem).</summary>
    public static bool Mentions(string label, string normalisedSentence) =>
        Concepts.First(item => item.Label == label).Terms.IsMatch(normalisedSentence);

    private const int CueEvidenceCharacters = 140;
}
