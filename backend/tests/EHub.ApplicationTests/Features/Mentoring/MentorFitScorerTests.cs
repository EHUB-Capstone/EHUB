using EHub.Application.Features.Mentoring;
using EHub.Domain.Entities;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorFitScorerTests
{
    [Theory]
    [InlineData("C#", "C# API", true)]
    [InlineData("C++", "C++ application", true)]
    [InlineData("Java", "JavaScript application", false)]
    [InlineData("Machine learning", "Machine maintenance", false)]
    [InlineData(".NET", "ASP.NET application", false)]
    public void ExactSkillsPreserveSymbolsAndRequireTheWholePhrase(string skill, string project, bool matches)
    {
        var result = MentorFitScorer.Score(new MentorProfile { Expertise = [skill] }, project, 0, 0);
        result.Reasons.Contains($"Chuyên môn phù hợp: {skill}").Should().Be(matches);
    }
    [Fact]
    public void RelevantExpertiseRanksAboveUnrelatedExpertise()
    {
        var aiMentor = new MentorProfile { Expertise = ["AI", "Data"], MaxTeams = 3 };
        var marketingMentor = new MentorProfile { Expertise = ["Marketing"], MaxTeams = 3 };

        var relevant = MentorFitScorer.Score(aiMentor, "AI data platform for student teams", 0.8, 0);
        var unrelated = MentorFitScorer.Score(marketingMentor, "AI data platform for student teams", 0.3, 0);

        relevant.Score.Should().BeGreaterThan(unrelated.Score);
        relevant.Reasons.Should().Contain(x => x.Contains("AI"));
    }

    [Fact]
    public void FullCapacityIsExplicitWithoutDistortingRelevanceScore()
    {
        var mentor = new MentorProfile { Expertise = ["Product"], MaxTeams = 1 };
        var available = MentorFitScorer.Score(mentor, "Product planning", 0.7, 0);
        var full = MentorFitScorer.Score(mentor, "Product planning", 0.7, 1);

        available.Score.Should().Be(full.Score);
        full.HasCapacity.Should().BeFalse();
        full.Reasons.Should().Contain("Đã đủ số nhóm");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(1.1)]
    public void InvalidSemanticSimilarityIsRejected(double similarity)
    {
        var act = () => MentorFitScorer.Score(new MentorProfile(), "AI", similarity, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
