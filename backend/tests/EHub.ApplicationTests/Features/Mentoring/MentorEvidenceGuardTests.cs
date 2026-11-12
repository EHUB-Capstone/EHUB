using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.Evidence;
using EHub.Domain.Entities;
using EHub.Infrastructure.Mentoring;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorEvidenceGuardTests
{
    [Theory]
    [InlineData("React frontend", "I have no React development experience", true)]
    [InlineData("Ứng dụng React", "Chưa có kinh nghiệm React", true)]
    [InlineData("Marketing strategy", "I have no React development experience", false)]
    [InlineData("Marketing; React is not required", "No React experience", false)]
    [InlineData("React frontend", "Not only React but also TypeScript", false)]
    [InlineData("React frontend", "Không chỉ React mà còn TypeScript", false)]
    [InlineData("Quantum photonic fabrication", "Quantum photonic fabrication", false)]
    public void OnlyAnExplicitDenialOfRequestedExpertiseBlocksTheCurrentAi(string project, string experience, bool blocked)
    {
        var mentor = new MentorProfile { Experience = experience, Expertise = ["React"] };
        var guard = new FileMentorEvidenceGuard(new ConfigurationBuilder().Build());

        guard.FindContradictions(project, [mentor], default).Contains(mentor.Id).Should().Be(blocked);
    }

    [Fact]
    public void MissingExplicitCatalogFailsSafelyWithoutUsingAModelArtifact()
    {
        var path = Path.Combine(Path.GetTempPath(), $"PRIVATE-CATALOG-{Guid.NewGuid():N}.json");
        var guard = new FileMentorEvidenceGuard(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Mentoring:Gemini:RuleCatalogPath"] = path }).Build());
        Action act = () => guard.FindContradictions("React", [new MentorProfile()], default);

        act.Should().Throw<MentorFitRankerUnavailableException>().Which.Message.Should().NotContain(path);
    }

    [Theory]
    [InlineData("{\"topics\":[]}")]
    [InlineData("{\"topics\":[{\"id\":\"a\",\"aliases\":[\"React\"],\"parent\":\"a\"}]}")]
    [InlineData("{\"topics\":[{\"id\":\"a\",\"aliases\":[\"React\"]},{\"id\":\"a\",\"aliases\":[\"Cloud\"]}]}")]
    public void InvalidCatalogsCannotDisableTheEvidenceGuard(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ehub-evidence-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            var guard = new FileMentorEvidenceGuard(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Mentoring:Gemini:RuleCatalogPath"] = path }).Build());
            Action act = () => guard.FindContradictions("React", [new MentorProfile()], default);
            act.Should().Throw<MentorFitRankerUnavailableException>();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SharedTextFilteringRetainsPositiveClaimsAndRemovesExplicitDenials()
    {
        const string text = "Có kinh nghiệm Marketing. Chưa có kinh nghiệm React.";
        MentorEvidenceText.Positive(text).Should().Contain("marketing").And.NotContain("react");
        MentorEvidenceText.Excluded(text).Should().Contain("react");
        MentorEvidenceText.Normalize("Chuyên môn ĐỔI MỚI").Should().Be("chuyen mon doi moi");
    }
}
