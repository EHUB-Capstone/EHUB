using EHub.Application.Features.ProposalAnalyses;
using EHub.Contracts.ProjectProposals;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.ProposalAnalyses;

public sealed class ProposalEmbeddingTextBuilderTests
{
    private readonly ProposalEmbeddingTextBuilder _builder = new();

    [Fact]
    public void Build_NormalizesWhitespaceAndUnicodeDeterministically()
    {
        var composed = _builder.Build(new ProjectProposalSnapshotDto
        {
            Title = "  Cà   phê\r\n thông minh ",
            Problem = "Xếp   hàng lâu"
        });
        var decomposed = _builder.Build(new ProjectProposalSnapshotDto
        {
            Title = "Ca\u0300 phe\u0302 thông minh",
            Problem = "Xếp hàng lâu"
        });

        composed.Text.Should().Be("TITLE: Cà phê thông minh\nPROBLEM: Xếp hàng lâu");
        decomposed.Text.Should().Be(composed.Text);
        decomposed.ContentHash.Should().Be(composed.ContentHash);
        composed.ContentHash.Should().HaveLength(64);
    }

    [Fact]
    public void Build_WhenContentChanges_ChangesHash()
    {
        var first = _builder.Build(new ProjectProposalSnapshotDto { Title = "A", Problem = "First" });
        var second = _builder.Build(new ProjectProposalSnapshotDto { Title = "A", Problem = "Second" });

        second.ContentHash.Should().NotBe(first.ContentHash);
    }

    [Fact]
    public void Build_TruncatesAtStableProviderLimit()
    {
        var proposal = new ProjectProposalSnapshotDto
        {
            Title = "Long proposal",
            Problem = new string('x', ProposalEmbeddingTextBuilder.MaximumCharacters),
            Solution = new string('y', 1_000)
        };

        var first = _builder.Build(proposal);
        var second = _builder.Build(proposal);

        first.WasTruncated.Should().BeTrue();
        first.Text.Length.Should().BeLessThanOrEqualTo(ProposalEmbeddingTextBuilder.MaximumCharacters);
        first.Should().Be(second);
    }
}
