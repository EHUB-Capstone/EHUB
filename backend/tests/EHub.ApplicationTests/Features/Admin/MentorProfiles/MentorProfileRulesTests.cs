using EHub.Application.Features.Admin.MentorProfiles;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Admin.MentorProfiles;

public sealed class MentorProfileRulesTests
{
    [Fact]
    public void NormalizeExpertise_TrimsCollapsesSpacesAndDropsEmptyEntries()
    {
        var (values, error) = MentorProfileRules.NormalizeExpertise(["  Marketing ", "", "  ", "Fundraising   pitch"]);

        error.Should().BeNull();
        values.Should().Equal("Marketing", "Fundraising pitch");
    }

    [Fact]
    public void NormalizeExpertise_AcceptsNothing()
    {
        MentorProfileRules.NormalizeExpertise(null).Values.Should().BeEmpty();
        MentorProfileRules.NormalizeExpertise([]).Error.Should().BeNull();
    }

    [Theory]
    [InlineData("AI", "ai")]
    [InlineData("Dữ liệu", "  Dữ   liệu ")]
    public void NormalizeExpertise_RejectsDuplicatesIgnoringCaseAndSpacing(string first, string second)
    {
        MentorProfileRules.NormalizeExpertise([first, second]).Error.Should().Contain("more than once");
    }

    [Fact]
    public void NormalizeExpertise_EnforcesTheLengthAndCountLimits()
    {
        MentorProfileRules.NormalizeExpertise(["A"]).Error.Should().Contain("2 to 50");
        MentorProfileRules.NormalizeExpertise([new string('x', 51)]).Error.Should().Contain("2 to 50");
        MentorProfileRules.NormalizeExpertise(Enumerable.Range(1, 20).Select(index => $"Skill {index}")).Error.Should().BeNull();
        MentorProfileRules.NormalizeExpertise(Enumerable.Range(1, 21).Select(index => $"Skill {index}")).Error.Should().Contain("at most 20");
    }

    [Theory]
    [InlineData("Startup domain")]
    [InlineData("Technology skill")]
    [InlineData("Mentor tag")]
    public void NormalizeTags_AppliesTheSameRulesToEveryKindAndNamesTheKindInTheMessage(string label)
    {
        MentorProfileRules.NormalizeTags(label, ["React", " react "]).Error.Should().StartWith(label).And.Contain("more than once");
        MentorProfileRules.NormalizeTags(label, ["A"]).Error.Should().Contain(label.ToLowerInvariant());
        MentorProfileRules.NormalizeTags(label, Enumerable.Range(1, 21).Select(index => $"Item {index}")).Error.Should().Contain(label.ToLowerInvariant());
        MentorProfileRules.NormalizeTags(label, ["  .NET ", "Machine   learning"]).Values.Should().Equal(".NET", "Machine learning");
    }

    [Fact]
    public void BioAndAvailabilityNote_AreLimitedAfterTrimming()
    {
        MentorProfileRules.ValidateBio(new string('b', 2000)).Should().BeNull();
        MentorProfileRules.ValidateBio(new string('b', 2001)).Should().NotBeNull();
        MentorProfileRules.ValidateBio(null).Should().BeNull();
        MentorProfileRules.ValidateAvailabilityNote(new string('a', 501)).Should().NotBeNull();
        MentorProfileRules.ValidateAvailabilityNote("  Weekdays  ").Should().BeNull();
    }

    [Fact]
    public void Clean_TurnsBlankIntoNull()
    {
        MentorProfileRules.Clean("   ").Should().BeNull();
        MentorProfileRules.Clean("  text ").Should().Be("text");
    }
}
