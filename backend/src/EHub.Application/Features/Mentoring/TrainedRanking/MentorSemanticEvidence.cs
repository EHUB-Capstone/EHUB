using System.Text.RegularExpressions;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Mentoring.TrainedRanking;

// Keep the wording for the multilingual encoder / evidence excerpts; remove denied clauses
// rather than turning "no AI experience" into a positive semantic match for AI.
public static class MentorSemanticEvidence
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

    public static string[] Experience(MentorProfile mentor) => Snippets(string.Join('\n',
        new[] { mentor.Experience }.Concat((mentor.Experiences ?? []).Take(32)
            .Select(x => x.Area + ": " + x.Notes))));

    public static string[] Snippets(string? text)
        => Prepare(text, project: false);

    public static string[] ProjectSnippets(string? text)
        => Prepare(text, project: true);

    private static string[] Prepare(string? text, bool project)
    {
        text = (text ?? "")[..Math.Min(text?.Length ?? 0, 6000)];
        text = Regex.Replace(text,
            @"[\w.+-]+@[\w.-]+\.[\p{L}]{2,}|https?://\S+|(?<!\d)(?:\+?\d[ ().-]*){9,15}(?!\d)",
            " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
        var clauses = Regex.Split(text,
            @"[;\r\n!?]+|\.(?=\s|$)|\b(?:nhưng|tuy nhiên|but|however)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
        return clauses.Select(x => x.Trim()).Where(x => x.Length >= 12)
            .Where(x => project ? !Regex.IsMatch(MentorRankingFeatures.Normalize(x),
                        @"\b(?:khong (?:can|co nhu cau)|not (?:needed|required)|out of scope|ngoai (?:pham vi|nhu cau))\b",
                        RegexOptions.CultureInvariant, Timeout) :
                        !string.IsNullOrWhiteSpace(MentorEvidenceText.Positive(x)) &&
                        string.IsNullOrWhiteSpace(MentorEvidenceText.Excluded(x)))
            .SelectMany(x => Chunk(x, 256)).Distinct(StringComparer.Ordinal).Take(12).ToArray();
    }

    private static IEnumerable<string> Chunk(string text, int maximum)
    {
        while (text.Length > maximum)
        {
            var end = text.LastIndexOf(' ', maximum, maximum);
            if (end < maximum / 2) end = maximum;
            yield return text[..end].Trim();
            text = text[end..].Trim();
        }
        if (text.Length >= 12) yield return text;
    }

    public static bool CanPromote(MentorRankingModel model, string projectText, MentorProfile mentor)
    {
        var project = MentorRankingFeatures.PrepareProject(model, projectText, mentor.MentorType);
        // Domain similarity alone must never invent a need for a technical mentor.
        if (mentor.MentorType == "IT" && project.CoreTopics.Count == 0) return false;
        var denied = MentorEvidenceText.Excluded(mentor.Experience + "\n" + mentor.Bio);
        // Excluded() still has the denial words. Read topic aliases without treating the
        // clause itself as a request, so an old declared tag cannot override the denial.
        var normalized = MentorRankingFeatures.Normalize(denied);
        if (model.Topics.Where(x => project.Topics.ContainsKey(x.Id)).Any(topic => topic.Aliases.Any(alias =>
            Regex.IsMatch(normalized, @"(?<![a-z0-9+#.])" + Regex.Escape(alias.Normalized) + @"(?![a-z0-9+#])",
                RegexOptions.CultureInvariant, Timeout)))) return false;
        var excludedProject = MentorEvidenceText.Excluded(projectText);
        // Explicitly excluding the entire business slot takes priority over semantic similarity.
        return mentor.MentorType != "Business" || !Regex.IsMatch(excludedProject,
            @"\b(?:kinh doanh|business|thuong mai)\b", RegexOptions.CultureInvariant, Timeout);
    }
}
