using System.Globalization;
using EHub.Application.Common.Interfaces.Services;

namespace EHub.Application.Features.Mentoring.Scoring;

/// <summary>Weights five semantic criteria once; reserves thirty points for local history/outcomes.</summary>
public static class MentorCombinedScoring
{
    public static MentorFitRanking Combine(MentorFitRanking gemini, MentorScoringOptions settings,
        bool hasContradiction = false)
    {
        if (!settings.IsValid()) throw new MentorFitRankerUnavailableException();
        if (hasContradiction)
            return new MentorFitRanking(0, ["Backend: hồ sơ phủ định chuyên môn được dự án yêu cầu; cần admin rà soát"], false, 0);
        var criteria = gemini.Criteria;
        if (criteria is null || !criteria.IsValid()) throw new MentorFitRankerUnavailableException();
        var hasCriteriaEvidence = criteria.Values.Any(value => value > 0);
        if (gemini.HasEvidence != hasCriteriaEvidence)
            throw new MentorFitRankerUnavailableException();
        var weights = settings.CriterionWeights;
        var local = gemini.HasEvidence
            ? Enumerable.Range(0, weights.Length).Sum(index => (double)weights[index] * criteria.Values[index]) / weights.Sum()
            : 0;
        var hasEvidence = local > 0;
        var raw = local * settings.ProfilePoints / 100d;
        var parts = new[] { "kinh nghiệm", "chuyên môn/tag", "lĩnh vực", "công nghệ", "giới thiệu/học vấn" };
        var detail = string.Join("; ", Enumerable.Range(0, weights.Length).Select(index =>
            $"{parts[index]} {criteria.Values[index]} × {weights[index]}"));
        var reasons = new List<string>
        {
            $"Điểm hồ sơ từ 5 tiêu chí AI: ({detail}) / {weights.Sum()} = {Format(local)}/100"
        };
        if (gemini.HasEvidence) reasons.AddRange(gemini.Reasons);
        else reasons.Add("AI chưa đưa mentor này vào danh sách đề xuất; điểm hồ sơ bằng 0, cần admin rà soát");
        reasons.Add($"Điểm nền: {settings.ProfilePoints}% × Hồ sơ {Format(local)} = {Format(raw)}; " +
                    "lịch sử và thành tích được cộng tối đa 30 điểm (15 điểm mỗi phần)");
        return new MentorFitRanking((int)Math.Round(raw, MidpointRounding.AwayFromZero),
            reasons.Distinct(StringComparer.Ordinal).ToArray(), hasEvidence, raw);
    }

    private static string Format(double number) => number.ToString("0.##", CultureInfo.InvariantCulture);
}
