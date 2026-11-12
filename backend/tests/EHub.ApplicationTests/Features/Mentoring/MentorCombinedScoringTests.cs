using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.History;
using EHub.Application.Features.Mentoring.Scoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Mentoring;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorCombinedScoringTests
{
    [Theory]
    [InlineData(100, 100, 70)]
    [InlineData(90, 80, 63)]
    [InlineData(35, 80, 25)]
    [InlineData(100, 0, 70)]
    [InlineData(0, 100, 0)]
    [InlineData(0, 0, 0)]
    public void BackendAppliesWeightsToGeminiCriteriaAndReservesThirtyPointsForHistory(int profile, int overall, int expected)
    {
        var result = MentorCombinedScoring.Combine(Fit(profile, overall), new());
        result.Score.Should().Be(expected);
        result.RankingValue.Should().BeApproximately(profile * 0.7, 1e-9);
    }

    [Fact]
    public void FiveDifferentSemanticAssessmentsAreWeightedByBackendNotByTheOverallGeminiScore()
    {
        var input = Fit(100, 80) with { Criteria = new(90, 80, 60, 70, 40) };
        var result = MentorCombinedScoring.Combine(input, new());
        result.Score.Should().Be(54); // Profile 76.5 * 70% = 53.55; no additional overall AI score.
        result.RankingValue.Should().Be(53.55);
        result.Reasons.Should().Contain(reason => reason.Contains("76.5/100"));
    }

    [Fact]
    public void ProfileAndLocalHistoryMakeUpAllOneHundredPointsWithoutAnOverallModelScore()
    {
        var history = new MentorHistoryEvidence(100, 100, 100, "PRIVATE_HISTORY");
        var profileAndHistory = MentorHistoryRanking.Apply(MentorCombinedScoring.Combine(Fit(100, 0), new()), history);
        var both = MentorHistoryRanking.Apply(MentorCombinedScoring.Combine(Fit(100, 100), new()), history);
        profileAndHistory.Score.Should().Be(100);
        both.Score.Should().Be(100);
        both.History!.BaseFitScore.Should().Be(70);
        both.History.HistoryBonus.Should().Be(15);
        both.History.AchievementBonus.Should().Be(15);
        string.Join(' ', both.Reasons).Should().NotContain("PRIVATE_HISTORY");
    }

    [Fact]
    public void BackendContradictionsOverrideEvenHighSemanticScoresAndHistoricalVolume()
    {
        var fit = MentorCombinedScoring.Combine(Fit(100, 100), new(), hasContradiction: true);
        var result = MentorHistoryRanking.Apply(fit, new MentorHistoryEvidence(500, 500, 500, "PRIVATE"));
        result.Score.Should().Be(0);
        result.HasEvidence.Should().BeFalse();
        result.History!.HistoryBonus.Should().Be(0);
        result.History.AchievementBonus.Should().Be(0);
    }

    [Fact]
    public void NinetyPointProfileWithTwoMentoredTeamsAndTwoAwardedProjectsScoresSeventyFive()
    {
        var result = MentorHistoryRanking.Apply(MentorCombinedScoring.Combine(Fit(90, 100), new()),
            new MentorHistoryEvidence(1000, 2, 2, ""));

        result.History!.BaseFitScore.Should().Be(63);
        result.History.HistoryBonus.Should().Be(6);
        result.History.AchievementBonus.Should().Be(6);
        result.Score.Should().Be(75);
    }

    [Fact]
    public void NotShortlistedMentorsNeverReceiveInventedProfileOrHistoryPoints()
    {
        var fit = MentorCombinedScoring.Combine(Fit(0, 0), new());
        MentorHistoryRanking.Apply(fit, new MentorHistoryEvidence(100, 100, 100, "")).Score.Should().Be(0);
    }

    [Fact]
    public void LegacyOverallScoresNeverChangeTheSameFiveCriterionRatings()
    {
        MentorCombinedScoring.Combine(Fit(90, 0), new()).Score.Should().Be(63);
        MentorCombinedScoring.Combine(Fit(90, 100), new()).Score.Should().Be(63);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidCriterionWeightsFailInsteadOfReturningAnInventedScore(int weight)
    {
        Action action = () => MentorCombinedScoring.Combine(Fit(80, 80), new() { ExperienceWeight = weight });
        action.Should().Throw<MentorFitRankerUnavailableException>();
    }

    [Fact]
    public void APreviousDeploymentShareSettingCannotKeepTheRemovedThirtyPercentTerm()
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["BackendWeightPercent"] = "70" }).Build().Get<MentorScoringOptions>()!;
        MentorCombinedScoring.Combine(Fit(100, 100), settings).Score.Should().Be(70);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void OutOfRangeCriteriaAreNotClampedIntoPlausibleScores(int value)
    {
        Action action = () => MentorCombinedScoring.Combine(Fit(80, 80) with { Criteria = new(value, 80, 80, 80, 80) }, new());
        action.Should().Throw<MentorFitRankerUnavailableException>();
    }

    [Fact]
    public void MissingCriteriaCannotFallBackToTheOldKeywordFormula()
    {
        Action action = () => MentorCombinedScoring.Combine(new(80, []), new());
        action.Should().Throw<MentorFitRankerUnavailableException>();
    }

    [Fact]
    public void AnOverallScoreWithoutAnyCriterionEvidenceIsRejected()
    {
        Action action = () => MentorCombinedScoring.Combine(Fit(0, 80) with { HasEvidence = true }, new());
        action.Should().Throw<MentorFitRankerUnavailableException>();
    }

    [Fact]
    public void DisablingACriterionRenormalizesRemainingWeightsWithoutAnotherGeminiRequest()
    {
        var settings = new MentorScoringOptions { ExpertiseWeight = 0, StartupDomainWeight = 0, TechnologyWeight = 0, BioWeight = 0 };
        var result = MentorCombinedScoring.Combine(Fit(100, 80) with { Criteria = new(100, 0, 0, 0, 0) }, settings);
        result.Score.Should().Be(70);
        new MentorScoringOptions { ExperienceWeight = 0, ExpertiseWeight = 0, StartupDomainWeight = 0, TechnologyWeight = 0, BioWeight = 0 }
            .IsValid().Should().BeFalse();
    }

    [Fact]
    public void KnownDenialsAreBlockedButUnknownVocabularyCanBeScoredSemantically()
    {
        var guard = new FileMentorEvidenceGuard(new ConfigurationBuilder().Build());
        var denied = new MentorProfile { Experience = "I have no React development experience", TechnologySkills = ["React"], Type = MentorType.Academic };
        guard.FindContradictions("React frontend", [denied], default).Should().Contain(denied.Id);
        var niche = new MentorProfile { Experience = "Quantum photonic fabrication" };
        guard.FindContradictions("Quantum photonic fabrication", [niche], default).Should().BeEmpty();
        MentorCombinedScoring.Combine(Fit(90, 80), new()).Score.Should().Be(63);
    }

    private static MentorFitRanking Fit(int profile, int overall) =>
        new(overall, [], profile > 0, overall) { Criteria = new(profile, profile, profile, profile, profile) };
}
