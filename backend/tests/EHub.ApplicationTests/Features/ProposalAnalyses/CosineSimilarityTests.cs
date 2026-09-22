using EHub.Application.Features.ProposalAnalyses;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.ProposalAnalyses;

public sealed class CosineSimilarityTests
{
    [Fact]
    public void Calculate_ReturnsOneForIdenticalVectors() =>
        CosineSimilarity.Calculate([1, 2, 3], [1, 2, 3]).Should().BeApproximately(1, 0.000001);

    [Fact]
    public void Calculate_ReturnsZeroForOrthogonalVectors() =>
        CosineSimilarity.Calculate([1, 0], [0, 1]).Should().BeApproximately(0, 0.000001);

    [Fact]
    public void Calculate_RejectsMismatchedDimensions() =>
        FluentActions.Invoking(() => CosineSimilarity.Calculate([1, 2], [1]))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Calculate_RejectsZeroAndNonFiniteVectors()
    {
        FluentActions.Invoking(() => CosineSimilarity.Calculate([0, 0], [1, 0]))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => CosineSimilarity.Calculate([float.NaN], [1]))
            .Should().Throw<ArgumentException>();
    }
}
