using EHub.Application.Features.ProposalAnalyses;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.ProposalAnalyses;

public sealed class ProposalRankingMetricsTests
{
    [Fact]
    public void Calculate_ReturnsStandardPrecisionRecallAndNdcgAtK()
    {
        var metrics = ProposalRankingMetrics.Calculate(
            ["relevant-high", "irrelevant", "relevant-low", "outside-k"],
            new Dictionary<string, int>
            {
                ["relevant-high"] = 3,
                ["relevant-low"] = 1
            },
            k: 3);

        metrics.PrecisionAtK.Should().BeApproximately(2d / 3d, 0.000001);
        metrics.RecallAtK.Should().Be(1);
        metrics.NdcgAtK.Should().BeGreaterThan(0.9).And.BeLessThan(1);
    }

    [Fact]
    public void Calculate_ReturnsPerfectMetricsForIdealRanking()
    {
        var metrics = ProposalRankingMetrics.Calculate(
            ["high", "medium", "irrelevant"],
            new Dictionary<string, int> { ["high"] = 3, ["medium"] = 2 },
            k: 2);

        metrics.Should().Be(new ProposalRankingMetricSet(1, 1, 1));
    }

    [Fact]
    public void Calculate_RejectsDuplicateRankedIdentifiers()
    {
        var action = () => ProposalRankingMetrics.Calculate(
            ["same", "same"],
            new Dictionary<string, int> { ["same"] = 1 },
            k: 2);

        action.Should().Throw<ArgumentException>();
    }
}
