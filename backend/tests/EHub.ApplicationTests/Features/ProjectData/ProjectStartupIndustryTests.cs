using EHub.Application.Features.ProjectData.Common;
using EHub.Application.Features.ProjectData.ManageStartupIndustries;
using EHub.Contracts.ProjectData;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.ProjectData;

public sealed class ProjectStartupIndustryTests
{
    [Fact]
    public void Tokens_ListAddedRemovedAndKeptIndustries()
    {
        var tokens = ProjectStartupIndustryHistoryParser.BuildTokens(
            ["EdTech", "Health Tech"], ["EdTech", "Fintech"]);

        tokens.Should().Equal("+Fintech", "-Health Tech", "=EdTech");

        var parsed = ProjectStartupIndustryHistoryParser.Parse(System.Text.Json.JsonSerializer.Serialize(tokens));
        parsed.Added.Should().Equal("Fintech");
        parsed.Removed.Should().Equal("Health Tech");
        parsed.Kept.Should().Equal("EdTech");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[\"+\",\"x\"]")]
    public void Parse_ReturnsNothingForUnreadableEntries(string? json)
    {
        var parsed = ProjectStartupIndustryHistoryParser.Parse(json);

        parsed.Added.Should().BeEmpty();
        parsed.Removed.Should().BeEmpty();
        parsed.Kept.Should().BeEmpty();
    }

    [Fact]
    public void Parse_KeepsIndustryNamesThatStartWithASymbol()
    {
        var parsed = ProjectStartupIndustryHistoryParser.Parse("[\"+-Odd name\",\"-+Other\"]");

        parsed.Added.Should().Equal("-Odd name");
        parsed.Removed.Should().Equal("+Other");
    }

    public static TheoryData<int, string, bool> ValidatorCases => new()
    {
        { 0, "1", false },
        { 1, "1", true },
        { 3, "42", true },
        { 4, "1", false },
        { 1, "", false },
        { 1, "abc", false },
    };

    [Theory]
    [MemberData(nameof(ValidatorCases))]
    public void Validator_RequiresOneToThreeIndustriesAndNumericRowVersion(int count, string rowVersion, bool expected)
    {
        var request = new UpdateProjectStartupIndustriesRequest
        {
            StartupIndustryIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray(),
            RowVersion = rowVersion,
        };

        new UpdateProjectStartupIndustriesRequestValidator().Validate(request).IsValid.Should().Be(expected);
    }

    [Fact]
    public void Validator_RejectsEmptyIndustryIds()
    {
        var request = new UpdateProjectStartupIndustriesRequest { StartupIndustryIds = [Guid.Empty], RowVersion = "1" };

        new UpdateProjectStartupIndustriesRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }
}
