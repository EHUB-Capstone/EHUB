using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.ProjectData.Common;
using EHub.Application.Features.ProjectData.GetProjectData;
using EHub.Application.Features.ProjectData.GetProjectDataFilterOptions;
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
        validator.Validate(new GetProjectDataRequest { SemesterId = Guid.Empty }).IsValid.Should().BeFalse();
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
        var update = await new UpdateProjectAchievementsCommandHandler(_context, Substitute.For<IDateTimeProvider>())
            .HandleAsync(Guid.NewGuid(), new UpdateProjectAchievementsRequest { RowVersion = "1" }, userId, role);

        foreach (var error in new[] { list.Error, options.Error, update.Error })
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

    [Fact]
    public void DistinctSorted_TrimsDropsBlanksAndMergesCaseVariantsDeterministically()
    {
        ProjectDataQuery.DistinctSorted([" fa26-g1 ", "FA26-G1", "", "  ", "FA26-G2", "fa26-g1"])
            .Should().Equal("FA26-G1", "FA26-G2");
        ProjectDataQuery.DistinctSorted([]).Should().BeEmpty();
    }
}
