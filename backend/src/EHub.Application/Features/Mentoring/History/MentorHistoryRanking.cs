using EHub.Application.Common.Interfaces.Services;

namespace EHub.Application.Features.Mentoring.History;

/// <summary>Recorded outcomes add at most 20 points, only after the model establishes project relevance.</summary>
public static class MentorHistoryRanking
{
    public static MentorFitRanking Apply(MentorFitRanking fit, MentorHistoryEvidence history)
    {
        // Volume alone cannot turn an unrelated mentor into a relevant match.
        var historyBonus = fit.HasEvidence
            ? Math.Min(5, history.MentoredTeamCount) + Math.Min(5, history.CompletedSessionCount / 2) : 0;
        var achievementBonus = fit.HasEvidence ? Math.Min(5, history.AchievementCount) * 2 : 0;
        var baseScore = fit.HasEvidence ? fit.Score : 0;
        var reasons = fit.Reasons.ToList();
        if (historyBonus > 0) reasons.Add($"Ưu tiên lịch sử hướng dẫn: +{historyBonus} điểm (tối đa 10)");
        if (achievementBonus > 0) reasons.Add($"Ưu tiên thành tích nhóm đã hướng dẫn: +{achievementBonus} điểm (tối đa 10)");
        return fit with
        {
            Score = Math.Min(100, baseScore + historyBonus + achievementBonus),
            RankingValue = fit.HasEvidence ? Math.Min(100, (fit.RankingValue ?? fit.Score) + historyBonus + achievementBonus) : 0,
            Reasons = reasons.ToArray(),
            History = new MentorHistoryRankingEvidence(baseScore, history.CompletedSessionCount,
                history.MentoredTeamCount, history.AchievementCount, historyBonus, achievementBonus)
        };
    }
}
