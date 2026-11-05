using EHub.Application.Features.ProjectData.Common;
using EHub.Application.Features.ProjectData.GetProjectData;
using EHub.Application.Features.ProjectData.ManageCompetitions;
using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.ProjectData;

public sealed class ProjectCompetitionTests
{
    private static ProjectCompetitionEntryRequest Entry(string competition = "FShark", int year = 2026, string stage = "Final", string? other = null) =>
        new() { Competition = competition, Year = year, Stage = stage, OtherName = other };

    private static UpdateProjectCompetitionsRequest Request(params ProjectCompetitionEntryRequest[] entries) =>
        new() { Entries = entries, RowVersion = "1" };

    [Fact]
    public void TheFixedListHasTheNineCompetitionsAndOthers()
    {
        ProjectCompetitionNames.All.Should().Equal(
            "SV.STARTUP", "Sinh viên Thế Hệ Mới VTV", "VietFuture Awards", "FTalent", "DBC", "FShark", "Startup Wheel", "DAVAS", "SURF", "Others");
        ProjectCompetitionStageNames.All.Should().Equal("Registered", "Qualifier", "Final");
        Enum.GetNames<CompetitionStage>().Should().Equal(ProjectCompetitionStageNames.All);
    }

    [Theory]
    [InlineData("fshark", "FShark")]
    [InlineData("  sv.startup ", "SV.STARTUP")]
    [InlineData("sinh viên thế hệ mới vtv", "Sinh viên Thế Hệ Mới VTV")]
    [InlineData("OTHERS", "Others")]
    [InlineData("FShark 2", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Canonicalize_MatchesTheListIgnoringCaseAndSpacing(string? input, string? expected) =>
        ProjectCompetitionMapping.Canonicalize(input).Should().Be(expected);

    [Theory]
    [InlineData("final", CompetitionStage.Final)]
    [InlineData(" Qualifier ", CompetitionStage.Qualifier)]
    [InlineData("REGISTERED", CompetitionStage.Registered)]
    [InlineData("0", null)]
    [InlineData("2", null)]
    [InlineData("99", null)]
    [InlineData("Winner", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseStage_AcceptsNamesOnly(string? input, CompetitionStage? expected) =>
        ProjectCompetitionMapping.ParseStage(input).Should().Be(expected);

    [Fact]
    public void OtherName_IsTrimmedAndCollapsedAndComparedIgnoringCase()
    {
        ProjectCompetitionMapping.CleanOtherName("  Tech    Cup ").Should().Be("Tech Cup");
        ProjectCompetitionMapping.CleanOtherName(" \t ").Should().BeNull();
        ProjectCompetitionMapping.CleanOtherName(null).Should().BeNull();
        ProjectCompetitionMapping.NormalizeOtherName("Tech Cup").Should().Be("tech cup");
        ProjectCompetitionMapping.NormalizeOtherName(null).Should().BeEmpty();
        ProjectCompetitionMapping.DisplayName("Others", "Tech Cup").Should().Be("Tech Cup");
        ProjectCompetitionMapping.DisplayName("Others", null).Should().Be("Others");
        ProjectCompetitionMapping.DisplayName("FShark", "ignored").Should().Be("FShark");
    }

    [Fact]
    public void UpdateValidator_ChecksTheShapeOfEveryEntry()
    {
        var validator = new UpdateProjectCompetitionsRequestValidator();

        validator.Validate(Request()).IsValid.Should().BeTrue();
        validator.Validate(Request(Entry(), Entry("others", 2025, "registered", "Tech Cup"))).IsValid.Should().BeTrue();
        validator.Validate(Request(Entry("Nope"))).IsValid.Should().BeFalse();
        validator.Validate(Request(Entry(stage: "Winner"))).IsValid.Should().BeFalse();
        validator.Validate(Request(Entry(stage: "1"))).IsValid.Should().BeFalse();
        validator.Validate(Request(Entry(year: 1999))).IsValid.Should().BeFalse();
        validator.Validate(Request(Entry(year: 2101))).IsValid.Should().BeFalse();
        validator.Validate(Request(Entry("Others", other: new string('x', 101)))).IsValid.Should().BeFalse();
        validator.Validate(new UpdateProjectCompetitionsRequest { Entries = [Entry()], RowVersion = "abc" }).IsValid.Should().BeFalse();
        validator.Validate(new UpdateProjectCompetitionsRequest { Entries = [Entry()], RowVersion = "" }).IsValid.Should().BeFalse();

        var tooMany = Enumerable.Range(0, ProjectCompetitionNames.MaxEntriesPerProject + 1)
            .Select(index => Entry(year: 2000 + index)).ToArray();
        validator.Validate(Request(tooMany)).IsValid.Should().BeFalse();
        validator.Validate(Request(tooMany.Take(ProjectCompetitionNames.MaxEntriesPerProject).ToArray())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ListValidator_AcceptsCompetitionFiltersAndRejectsUnknownValues()
    {
        var validator = new GetProjectDataRequestValidator();

        validator.Validate(new GetProjectDataRequest
        {
            Competition = ["FShark", "sv.startup", "Others"],
            CompetitionStage = ["Final", "qualifier"],
            CompetitionYear = [2025, 2026],
        }).IsValid.Should().BeTrue();

        validator.Validate(new GetProjectDataRequest { Competition = ["FShark", "Nope"] }).IsValid.Should().BeFalse();
        validator.Validate(new GetProjectDataRequest { CompetitionStage = ["Final", "Winner"] }).IsValid.Should().BeFalse();
        validator.Validate(new GetProjectDataRequest { CompetitionStage = ["2"] }).IsValid.Should().BeFalse();
        validator.Validate(new GetProjectDataRequest { CompetitionYear = [1999] }).IsValid.Should().BeFalse();
        validator.Validate(new GetProjectDataRequest { CompetitionYear = [2026, 10000] }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EntryFilter_IsEmptyWithoutCompetitionFiltersAndMatchesOneEntryAtATime()
    {
        ProjectCompetitionMapping.EntryFilter(new GetProjectDataRequest()).Should().BeNull();
        ProjectCompetitionMapping.EntryFilter(new GetProjectDataRequest { Competition = [" "] }).Should().BeNull();

        var filter = ProjectCompetitionMapping.EntryFilter(new GetProjectDataRequest
        {
            Competition = ["fshark"],
            CompetitionStage = ["Final"],
            CompetitionYear = [2026],
        })!.Compile();

        EHub.Domain.Entities.ProjectCompetitionEntry Make(string competition, int year, CompetitionStage stage) =>
            new() { Competition = competition, Year = year, Stage = stage };

        filter(Make("FShark", 2026, CompetitionStage.Final)).Should().BeTrue();
        filter(Make("FShark", 2025, CompetitionStage.Final)).Should().BeFalse();
        filter(Make("FShark", 2026, CompetitionStage.Qualifier)).Should().BeFalse();
        filter(Make("DBC", 2026, CompetitionStage.Final)).Should().BeFalse();
    }
}
