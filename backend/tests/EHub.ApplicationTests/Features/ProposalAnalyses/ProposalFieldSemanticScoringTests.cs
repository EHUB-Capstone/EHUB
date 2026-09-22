using EHub.Application.Features.ProposalAnalyses;
using EHub.Contracts.ProjectProposals;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.ProposalAnalyses;

public sealed class ProposalFieldSemanticScoringTests
{
    [Fact]
    public void FieldTextBuilder_ProducesStableSeparatedRepresentations()
    {
        var builder = new ProposalFieldEmbeddingTextBuilder();
        var proposal = new ProjectProposalSnapshotDto
        {
            Problem = "  Sinh viên  khó tìm phòng. ",
            Solution = "Gợi ý phòng phù hợp.",
            TargetCustomers = "Sinh viên đại học.",
            ValueProposition = "Giảm thời gian tìm kiếm.",
            BusinessModel = "Thu phí đăng ký.",
            Technology = "Semantic search."
        };

        var first = builder.Build(proposal);
        var second = builder.Build(proposal);

        first.Should().HaveCount(4);
        first.Select(item => item.Field).Should().Equal(
            ProjectProposalSemanticField.Problem,
            ProjectProposalSemanticField.Solution,
            ProjectProposalSemanticField.TargetCustomers,
            ProjectProposalSemanticField.ValueAndApproach);
        first[0].Text.Should().Be("PROBLEM: Sinh viên khó tìm phòng.");
        first[3].Text.Should().Contain("VALUE_PROPOSITION: Giảm thời gian tìm kiếm.")
            .And.Contain("BUSINESS_MODEL: Thu phí đăng ký.")
            .And.Contain("TECHNOLOGY: Semantic search.");
        second.Should().Equal(first);
    }

    [Fact]
    public void WeightedScore_UsesVersionedThirtyThirtyTwentyTwentyProfile()
    {
        var score = WeightedSemanticSimilarity.Calculate(new ProposalFieldSimilarityScores(
            Problem: 0.9,
            Solution: 0.7,
            TargetCustomers: 0.8,
            ValueAndApproach: 0.6));

        score.Should().BeApproximately(0.76, 0.000001);
        WeightedSemanticSimilarity.CurrentWeights.Should().Be(new ProposalFieldSimilarityWeights(0.3, 0.3, 0.2, 0.2));
        WeightedSemanticSimilarity.CurrentVersion.Should().Be("proposal-field-weighted-semantic-v1");
    }

    [Fact]
    public void WeightedScore_RejectsInvalidSignals()
    {
        var action = () => WeightedSemanticSimilarity.Calculate(new ProposalFieldSimilarityScores(
            double.NaN,
            0.5,
            0.5,
            0.5));

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
