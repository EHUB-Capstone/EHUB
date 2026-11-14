using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.History;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorHistoryRankingTests
{
    [Fact]
    public void RecordedCoachingAndTeamOutcomesCanPrioritizeAnOtherwiseComparableRelevantMentor()
    {
        var experienced = MentorHistoryRanking.Apply(new MentorFitRanking(80, ["Relevant software background"], true, 80.2),
            new MentorHistoryEvidence(10, 2, 2, "PRIVATE_HISTORY"));
        var newcomer = MentorHistoryRanking.Apply(new MentorFitRanking(85, ["Relevant software background"], true, 85.1),
            new MentorHistoryEvidence(0, 0, 0, ""));

        experienced.Score.Should().Be(92);
        experienced.RankingValue.Should().BeGreaterThan(newcomer.RankingValue!.Value);
        experienced.History.Should().Be(new MentorHistoryRankingEvidence(80, 10, 2, 2, 6, 6));
        string.Join(' ', experienced.Reasons).Should().NotContain("PRIVATE_HISTORY");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    public void CompletedSessionVolumeDoesNotChangeTheScore(int sessions)
    {
        var result = MentorHistoryRanking.Apply(new MentorFitRanking(70, [], true, 70.4),
            new MentorHistoryEvidence(sessions, 2, 0, ""));

        result.Score.Should().Be(76);
        result.RankingValue.Should().Be(76.4);
        result.History!.HistoryBonus.Should().Be(6);

        var noTeams = MentorHistoryRanking.Apply(new MentorFitRanking(70, []),
            new MentorHistoryEvidence(sessions, 0, 0, ""));
        noTeams.Score.Should().Be(70);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 3)]
    [InlineData(4, 12)]
    [InlineData(5, 15)]
    [InlineData(1000, 15)]
    public void EachRecordedTeamAddsThreeHistoryPointsUpToFifteen(int teams, int bonus)
    {
        var result = MentorHistoryRanking.Apply(new MentorFitRanking(70, []),
            new MentorHistoryEvidence(0, teams, 0, ""));

        result.Score.Should().Be(70 + bonus);
        result.History!.HistoryBonus.Should().Be(bonus);
    }

    [Fact]
    public void AnUnrelatedMentorNeverGainsRelevanceFromLargeHistoricalCounts()
    {
        var result = MentorHistoryRanking.Apply(new MentorFitRanking(12, ["No matching evidence"], false, 12.1),
            new MentorHistoryEvidence(500, 500, 500, "Unrelated track record"));

        result.Score.Should().Be(0);
        result.RankingValue.Should().Be(0);
        result.HasEvidence.Should().BeFalse();
        result.History!.HistoryBonus.Should().Be(0);
        result.History.AchievementBonus.Should().Be(0);
        result.History.AchievementCount.Should().Be(500, "the admin still sees the recorded facts without an invented fit score");
    }

    [Fact]
    public void HistoryIsBoundedAndTheFinalScoreNeverExceedsOneHundred()
    {
        var result = MentorHistoryRanking.Apply(new MentorFitRanking(95, [], true, 95.2),
            new MentorHistoryEvidence(1000, 1000, 1000, ""));

        result.Score.Should().Be(100);
        result.RankingValue.Should().Be(100);
        result.History!.HistoryBonus.Should().Be(15);
        result.History.AchievementBonus.Should().Be(15);
        result.History.BaseFitScore.Should().Be(95);
    }

    [Fact]
    public void ANewMentorKeepsTheOriginalFitRatherThanReceivingANegativeHistoryPenalty()
    {
        var fit = new MentorFitRanking(80, ["Relevant expertise"], true, 80.35);
        var result = MentorHistoryRanking.Apply(fit, new MentorHistoryEvidence(0, 0, 0, ""));

        result.Score.Should().Be(fit.Score);
        result.RankingValue.Should().Be(fit.RankingValue);
        result.Reasons.Should().Equal(fit.Reasons);
    }
}
