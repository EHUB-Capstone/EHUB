using System.Text.RegularExpressions;
using System.Text;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Mentoring;

public static class MentorFitScorer
{
    // Ranking score, not a calibrated probability of a successful match.
    public static (int Score, string[] Reasons, bool HasCapacity) Score(MentorProfile profile,
        string projectText, double semanticSimilarity, int activeTeamCount)
    {
        if (!double.IsFinite(semanticSimilarity) || semanticSimilarity < -1 || semanticSimilarity > 1)
            throw new ArgumentOutOfRangeException(nameof(semanticSimilarity));

        var normalizedProject = Normalize(projectText);
        var expertiseMatches = profile.Expertise.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(skill => Regex.IsMatch(normalizedProject,
                @"(?<![\p{L}\p{N}+#.])" + Regex.Escape(Normalize(skill)) + @"(?![\p{L}\p{N}+#])",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))).ToArray();
        var keywordFit = Math.Min(1, expertiseMatches.Length / 2.0);
        var semanticFit = Math.Max(0, semanticSimilarity);
        var score = (int)Math.Round(100 * (0.7 * semanticFit + 0.3 * keywordFit),
            MidpointRounding.AwayFromZero);
        var hasCapacity = true;
        var reasons = new List<string>();
        if (semanticFit >= 0.5) reasons.Add("Hồ sơ có nội dung liên quan đến dự án");
        reasons.AddRange(expertiseMatches.Select(x => $"Chuyên môn phù hợp: {x}"));
        reasons.Add($"Đang phụ trách {activeTeamCount} nhóm");
        return (score, reasons.ToArray(), hasCapacity);
    }

    private static string Normalize(string value) => Regex.Replace(
        value.Normalize(NormalizationForm.FormC).ToLowerInvariant().Trim(), @"\s+", " ");
}
