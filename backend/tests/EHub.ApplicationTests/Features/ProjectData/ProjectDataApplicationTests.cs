using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.ProjectData.Common;
using EHub.Application.Features.ProjectData.GetProjectAchievementHistory;
using EHub.Application.Features.ProjectData.GetProjectData;
using EHub.Application.Features.ProjectData.GetProjectDataFilterOptions;
using EHub.Application.Features.ProjectData.GetProjectDataSummary;
using EHub.Application.Features.ProjectData.ManageAchievements;
using EHub.Contracts.ProjectData;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.ProjectData;

public sealed class ProjectDataApplicationTests
{
    private readonly IApplicationDbContext _context = Substitute.For<IApplicationDbContext>();

    [Theory]
    [InlineData(1, 20, null, null, null, true)]
    [InlineData(1, 10, "health", "projectName", "Potential", true)]
    [InlineData(3, 100, null, "semester", "funded", true)]
    [InlineData(0, 20, null, null, null, false)]
    [InlineData(1, 7, null, null, null, false)]
    [InlineData(1, 101, null, null, null, false)]
    [InlineData(1, 20, null, "classCode", null, true)]
    [InlineData(1, 20, null, "group", null, true)]
    [InlineData(1, 20, null, "subject", null, false)]
    [InlineData(1, 20, null, "password", null, false)]
    [InlineData(1, 20, null, null, "Famous", false)]
    public void ListValidator_AcceptsOnlyAllowedPagingSortAndAchievementValues(
        int pageIndex, int pageSize, string? search, string? sortBy, string? achievement, bool expected)
    {
        var request = new GetProjectDataRequest
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Search = search,
            SortBy = sortBy,
            Achievement = achievement,
        };

        new GetProjectDataRequestValidator().Validate(request).IsValid.Should().Be(expected);
    }

    [Fact]
    public void ListValidator_RejectsOverlongSearchAndEmptyIds()
    {
        var validator = new GetProjectDataRequestValidator();
        validator.Validate(new GetProjectDataRequest { Search = new string('x', 101) }).IsValid.Should().BeFalse();
        validator.Validate(new GetProjectDataRequest { Search = new string('x', 100) }).IsValid.Should().BeTrue();
        validator.Validate(new GetProjectDataRequest { Semester = "FA", Year = 2026 }).IsValid.Should().BeTrue();
        validator.Validate(new GetProjectDataRequest { Semester = " su " }).IsValid.Should().BeTrue();
        validator.Validate(new GetProjectDataRequest { Semester = "XX" }).IsValid.Should().BeFalse();
        validator.Validate(new GetProjectDataRequest { Year = 1999 }).IsValid.Should().BeFalse();
        validator.Validate(new GetProjectDataRequest { Year = 10000 }).IsValid.Should().BeFalse();
        validator.Validate(new GetProjectDataRequest { LecturerId = Guid.Empty }).IsValid.Should().BeFalse();
        validator.Validate(new GetProjectDataRequest { MentorId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("1", new[] { "Potential" }, true)]
    [InlineData("42", new string[0], true)]
    [InlineData("42", new[] { "potential", "FUNDED", "Awarded" }, true)]
    [InlineData("", new[] { "Potential" }, false)]
    [InlineData("abc", new[] { "Potential" }, false)]
    [InlineData("1", new[] { "Famous" }, false)]
    [InlineData("1", new[] { "Potential", "Funded", "Awarded", "Potential" }, false)]
    public void UpdateValidator_RequiresKnownLabelsAndNumericRowVersion(
        string rowVersion, string[] achievements, bool expected)
    {
        var request = new UpdateProjectAchievementsRequest { RowVersion = rowVersion, Achievements = achievements };

        new UpdateProjectAchievementsRequestValidator().Validate(request).IsValid.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, new[] { "Potential" }, true)]
    [InlineData("  Strong pilot  ", new[] { "Potential", "Funded" }, true)]
    [InlineData("", new string[0], true)]
    [InlineData("   ", new string[0], true)]
    [InlineData("A note without labels", new string[0], false)]
    public void UpdateValidator_AllowsANoteOnlyTogetherWithAtLeastOneLabel(string? note, string[] achievements, bool expected)
    {
        var request = new UpdateProjectAchievementsRequest { RowVersion = "1", Achievements = achievements, Note = note };

        new UpdateProjectAchievementsRequestValidator().Validate(request).IsValid.Should().Be(expected);
    }

    [Fact]
    public void UpdateValidator_LimitsTheNoteLength()
    {
        var validator = new UpdateProjectAchievementsRequestValidator();
        UpdateProjectAchievementsRequest WithNote(string note) =>
            new() { RowVersion = "1", Achievements = ["Potential"], Note = note };

        validator.Validate(WithNote(new string('n', 500))).IsValid.Should().BeTrue();
        validator.Validate(WithNote(new string('n', 501))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateHandler_RejectsAnOrphanNoteBeforeTouchingData()
    {
        var handler = new UpdateProjectAchievementsCommandHandler(_context, Substitute.For<IDateTimeProvider>());

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            new UpdateProjectAchievementsRequest { RowVersion = "1", Achievements = [], Note = "Why?" },
            Guid.NewGuid(),
            SystemRoles.Admin);

        result.Error.Code.Should().Be(ErrorCodes.ProjectDataValidationError);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  keep me  ", "keep me")]
    public void NormalizeNote_TrimsAndTreatsBlankAsNoNote(string? input, string? expected)
    {
        ProjectAchievementMapping.NormalizeNote(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    [InlineData("")]
    public async Task Handlers_DenyRolesOutsideAdminAndLecturerBeforeTouchingData(string role)
    {
        var userId = Guid.NewGuid();

        var list = await new GetProjectDataQueryHandler(_context)
            .HandleAsync(new GetProjectDataRequest(), userId, role);
        var options = await new GetProjectDataFilterOptionsQueryHandler(_context)
            .HandleAsync(userId, role);
        var summary = await new GetProjectDataSummaryQueryHandler(_context)
            .HandleAsync(new GetProjectDataRequest(), userId, role);
        var update = await new UpdateProjectAchievementsCommandHandler(_context, Substitute.For<IDateTimeProvider>())
            .HandleAsync(Guid.NewGuid(), new UpdateProjectAchievementsRequest { RowVersion = "1" }, userId, role);
        var history = await new GetProjectAchievementHistoryQueryHandler(_context)
            .HandleAsync(Guid.NewGuid(), userId, role);

        foreach (var error in new[] { list.Error, options.Error, summary.Error, update.Error, history.Error })
            error.Code.Should().Be(ErrorCodes.ProjectDataAccessDenied);
        _ = _context.DidNotReceiveWithAnyArgs().Projects;
    }

    [Fact]
    public async Task ListHandler_RejectsInvalidPagingWhenCalledDirectly()
    {
        var result = await new GetProjectDataQueryHandler(_context)
            .HandleAsync(new GetProjectDataRequest { PageIndex = 0 }, Guid.NewGuid(), SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ProjectDataValidationError);
    }

    [Fact]
    public async Task UpdateHandler_RejectsMalformedVersionAndUnknownLabelsBeforeTouchingData()
    {
        var handler = new UpdateProjectAchievementsCommandHandler(_context, Substitute.For<IDateTimeProvider>());

        var badVersion = await handler.HandleAsync(
            Guid.NewGuid(), new UpdateProjectAchievementsRequest { RowVersion = "x" }, Guid.NewGuid(), SystemRoles.Admin);
        var badLabel = await handler.HandleAsync(
            Guid.NewGuid(),
            new UpdateProjectAchievementsRequest { RowVersion = "1", Achievements = ["Famous"] },
            Guid.NewGuid(),
            SystemRoles.Lecturer);

        badVersion.Error.Code.Should().Be(ErrorCodes.ProjectDataValidationError);
        badLabel.Error.Code.Should().Be(ErrorCodes.ProjectDataValidationError);
    }

    [Fact]
    public void AchievementMapping_ReturnsIndependentLabelsInStableOrder()
    {
        ProjectAchievementMapping.ToNames(false, false, false).Should().BeEmpty();
        ProjectAchievementMapping.ToNames(true, false, true).Should().Equal("Potential", "Awarded");
        ProjectAchievementMapping.ToNames(true, true, true).Should().Equal("Potential", "Funded", "Awarded");
        ProjectAchievementMapping.Canonicalize(" funded ").Should().Be("Funded");
        ProjectAchievementMapping.Canonicalize("other").Should().BeNull();
        ProjectAchievementMapping.Canonicalize(null).Should().BeNull();
    }

    [Theory]
    [InlineData("SP", EHub.Domain.Enums.SemesterTerm.Spring)]
    [InlineData("su", EHub.Domain.Enums.SemesterTerm.Summer)]
    [InlineData(" FA ", EHub.Domain.Enums.SemesterTerm.Fall)]
    public void ParseTerm_MapsShortCodesToTerms(string code, EHub.Domain.Enums.SemesterTerm expected)
    {
        ProjectDataQuery.ParseTerm(code).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("Fall")]
    public void ParseTerm_ReturnsNullForUnknownCodes(string? code)
    {
        ProjectDataQuery.ParseTerm(code).Should().BeNull();
    }

    [Fact]
    public void HistoryTokens_RoundTripAddedRemovedKeptAndNote()
    {
        var tokens = ProjectAchievementHistoryParser.BuildTokens(
            ["Potential", "Funded"], ["Potential", "Awarded"], noteChanged: true, note: "Won the showcase");

        tokens.Should().Equal("+Awarded", "-Funded", "=Potential", "note=Won the showcase");
        var parsed = ProjectAchievementHistoryParser.Parse("PROJECT_ACHIEVEMENTS_CHANGED", "ignored", System.Text.Json.JsonSerializer.Serialize(tokens));
        parsed.Added.Should().Equal("Awarded");
        parsed.Removed.Should().Equal("Funded");
        parsed.Kept.Should().Equal("Potential");
        parsed.NoteChanged.Should().BeTrue();
        parsed.Note.Should().Be("Won the showcase");
    }

    [Fact]
    public void HistoryTokens_DistinguishClearingTheNoteFromLeavingItAlone()
    {
        var cleared = ProjectAchievementHistoryParser.Parse("PROJECT_ACHIEVEMENTS_CHANGED", "",
            System.Text.Json.JsonSerializer.Serialize(ProjectAchievementHistoryParser.BuildTokens(["Potential"], [], true, null)));
        cleared.Removed.Should().Equal("Potential");
        cleared.NoteChanged.Should().BeTrue();
        cleared.Note.Should().BeNull();

        var untouched = ProjectAchievementHistoryParser.Parse("PROJECT_ACHIEVEMENTS_CHANGED", "",
            System.Text.Json.JsonSerializer.Serialize(ProjectAchievementHistoryParser.BuildTokens(["Potential"], ["Potential", "Funded"], false, "kept")));
        untouched.Added.Should().Equal("Funded");
        untouched.Kept.Should().Equal("Potential");
        untouched.NoteChanged.Should().BeFalse();
    }

    [Theory]
    [InlineData("Achievements changed from none to Potential. Note: \"Strong pilot\"", "[\"Potential\",\"note\"]", new[] { "Potential" }, new string[0], new string[0], true, "Strong pilot")]
    [InlineData("Achievements changed from Potential to Potential, Funded.", "[\"Funded\"]", new[] { "Funded" }, new string[0], new[] { "Potential" }, false, null)]
    [InlineData("Achievements changed from Potential, Funded to none.", "[\"Potential\",\"Funded\"]", new string[0], new[] { "Potential", "Funded" }, new string[0], false, null)]
    [InlineData("Achievement note updated. Note: \"New reason\"", "[\"note\"]", new string[0], new string[0], new string[0], true, "New reason")]
    public void LegacyEntries_AreRecoveredFromTheirSummarySentence(
        string summary, string json, string[] added, string[] removed, string[] kept, bool noteChanged, string? note)
    {
        var parsed = ProjectAchievementHistoryParser.Parse("PROJECT_ACHIEVEMENTS_CHANGED", summary, json);

        parsed.Added.Should().Equal(added);
        parsed.Removed.Should().Equal(removed);
        parsed.Kept.Should().Equal(kept);
        parsed.NoteChanged.Should().Be(noteChanged);
        parsed.Note.Should().Be(note);
    }

    [Fact]
    public void LegacyCarriedOverEntries_ShowTheirLabelsAsAdded()
    {
        var parsed = ProjectAchievementHistoryParser.Parse(
            "ACHIEVEMENTS_CARRIED_OVER", "Carried over achievements (Potential, Funded) from the previous semester.", "[\"Potential\",\"Funded\"]");

        parsed.Added.Should().Equal("Potential", "Funded");
        parsed.Removed.Should().BeEmpty();
        parsed.NoteChanged.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Profile edit", "not json")]
    [InlineData("Something unexpected", "[\"x\"]")]
    public void UnreadableEntries_ComeBackEmptyForTheClientToShowTheSummary(string summary, string json)
    {
        var parsed = ProjectAchievementHistoryParser.Parse("PROJECT_ACHIEVEMENTS_CHANGED", summary, json);

        parsed.Added.Should().BeEmpty();
        parsed.Removed.Should().BeEmpty();
        parsed.Kept.Should().BeEmpty();
        parsed.NoteChanged.Should().BeFalse();
    }

    [Fact]
    public void DistinctSortedNaturally_OrdersNumericSuffixesByValue()
    {
        ProjectDataQuery.DistinctSortedNaturally(["EXE201g_7G10", "EXE201g_7G2", " exe201g_7g2 ", "EXE201g_7G1", "", "EXE201g_10G1", "EXE201g_7G11"])
            .Should().Equal("EXE201g_7G1", "EXE201g_7G2", "EXE201g_7G10", "EXE201g_7G11", "EXE201g_10G1");
        ProjectDataQuery.DistinctSortedNaturally([]).Should().BeEmpty();
    }

    [Theory]
    [InlineData("G2", "G10", -1)]
    [InlineData("G10", "G2", 1)]
    [InlineData("g2", "G2", 0)]
    [InlineData("G02", "G2", 0)]
    [InlineData("EXE101_2", "EXE101_10", -1)]
    [InlineData("A", "A1", -1)]
    public void NaturalComparer_ComparesDigitRunsNumericallyAndIgnoresCase(string left, string right, int expectedSign)
    {
        Math.Sign(NaturalStringComparer.Instance.Compare(left, right)).Should().Be(expectedSign);
    }

    [Fact]
    public void DistinctSorted_TrimsDropsBlanksAndMergesCaseVariantsDeterministically()
    {
        ProjectDataQuery.DistinctSorted([" fa26-g1 ", "FA26-G1", "", "  ", "FA26-G2", "fa26-g1"])
            .Should().Equal("FA26-G1", "FA26-G2");
        ProjectDataQuery.DistinctSorted([]).Should().BeEmpty();
    }
}
