using EHub.Infrastructure.Mentoring;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class VectorSimilarityTests
{
    [Fact]
    public void CosineIsScaleIndependentAndHandlesOppositeVectors()
    {
        OllamaMentorEmbeddingSearch.Cosine([2, 0], [5, 0]).Should().BeApproximately(1, 1e-10);
        OllamaMentorEmbeddingSearch.Cosine([2, 0], [0, 5]).Should().BeApproximately(0, 1e-10);
        OllamaMentorEmbeddingSearch.Cosine([2, 0], [-5, 0]).Should().BeApproximately(-1, 1e-10);
    }

    [Fact]
    public void CosineRejectsDifferentDimensionsAndHandlesZeroVector()
    {
        OllamaMentorEmbeddingSearch.Cosine([0, 0], [1, 0]).Should().Be(0);
        var action = () => OllamaMentorEmbeddingSearch.Cosine([1], [1, 2]);
        action.Should().Throw<ArgumentException>();
    }
}
