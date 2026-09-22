using EHub.Application.Features.ProposalAnalyses;
using EHub.Contracts.ProjectProposals;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.ProposalAnalyses;

public sealed class ProposalHybridSimilarityTests
{
    [Fact]
    public void LexicalScore_ReturnsOneForEquivalentVietnameseUnicodeText()
    {
        var current = Proposal(problem: "Học sinh cần giải pháp tiết kiệm nước sạch.");
        var equivalent = Proposal(problem: "Học sinh cần giải pháp tiết kiệm nước sạch.");

        var score = ProposalLexicalSimilarity.Calculate(current, [equivalent]).Single();

        score.TfIdf.Should().BeApproximately(1, 0.000001);
        score.Jaccard.Should().BeApproximately(1, 0.000001);
        ProposalLexicalSimilarity.CurrentVersion.Should().Be("proposal-lexical-unigram-bigram-v1");
    }

    [Fact]
    public void LexicalScore_ReturnsZeroWhenDocumentsShareNoFeatures()
    {
        var current = Proposal(problem: "solar water platform");
        var unrelated = Proposal(problem: "medical clinic scheduling");

        var score = ProposalLexicalSimilarity.Calculate(current, [unrelated]).Single();

        score.TfIdf.Should().Be(0);
        score.Jaccard.Should().Be(0);
    }

    [Fact]
    public void LexicalScore_IsComputedIndependentlyForEveryCandidate()
    {
        var current = Proposal(problem: "student housing search", solution: "verified room matching");
        var similar = Proposal(problem: "student housing search", solution: "verified room recommendation");
        var unrelated = Proposal(problem: "crop disease detection", solution: "drone image analysis");

        var scores = ProposalLexicalSimilarity.Calculate(current, [similar, unrelated]);

        scores.Should().HaveCount(2);
        scores[0].TfIdf.Should().BeGreaterThan(scores[1].TfIdf);
        scores[0].Jaccard.Should().BeGreaterThan(scores[1].Jaccard);
    }

    [Fact]
    public void HybridScore_UsesVersionedSeventyTwentyTenProfile()
    {
        var score = HybridProposalSimilarity.Calculate(
            weightedSemanticSimilarity: 0.8,
            tfIdfSimilarity: 0.5,
            jaccardSimilarity: 0.25);

        score.Should().BeApproximately(0.685, 0.000001);
        HybridProposalSimilarity.CurrentWeights.Should().Be(new ProposalHybridSimilarityWeights(0.7, 0.2, 0.1));
        HybridProposalSimilarity.CurrentVersion.Should().Be("proposal-hybrid-semantic-tfidf-jaccard-v1");
    }

    [Fact]
    public void HybridScore_RejectsLexicalSignalsOutsideUnitRange()
    {
        var action = () => HybridProposalSimilarity.Calculate(0.5, 1.01, 0.5);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static ProjectProposalSnapshotDto Proposal(string problem, string solution = "") => new()
    {
        Problem = problem,
        Solution = solution
    };
}
