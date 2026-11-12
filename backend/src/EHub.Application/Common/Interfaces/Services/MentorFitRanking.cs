namespace EHub.Application.Common.Interfaces.Services;

public enum MentorRankingScope
{
    MentorRole,
    AllSpecialties
}

// A relative ranking score, not a calibrated probability of successful mentoring.
public sealed record MentorFitRanking(int Score, string[] Reasons, bool HasEvidence = true, double? RankingValue = null,
    MentorHistoryRankingEvidence? History = null)
{
    // Internal scoring input, not an API contract. Gemini supplies semantic assessments; Application applies weights.
    public MentorProfileFitCriteria? Criteria { get; init; }
}

public sealed record MentorProfileFitCriteria(int Experience, int Expertise, int StartupDomain, int Technology, int Bio)
{
    public static MentorProfileFitCriteria Empty { get; } = new(0, 0, 0, 0, 0);
    public int[] Values => [Experience, Expertise, StartupDomain, Technology, Bio];
    public bool IsValid() => Values.All(value => value is >= 0 and <= 100);
}

public sealed record MentorHistoryRankingEvidence(int BaseFitScore, int CompletedSessionCount,
    int MentoredTeamCount, int AchievementCount, int HistoryBonus, int AchievementBonus);
