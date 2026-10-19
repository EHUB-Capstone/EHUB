using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.ProjectData.GetProjectDataDistribution;
using EHub.Contracts.ProjectData;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.ProjectData;

public sealed class ProjectDataDistributionTests
{
    private readonly IApplicationDbContext _context = Substitute.For<IApplicationDbContext>();

    [Theory]
    [InlineData("BIT_SE", null, "BIT_SE")]
    [InlineData(" bba_mkt ", "BIT_AI", "BBA_MKT")]
    [InlineData("UNDECLARED", "BIT_AI", "BIT_AI")]
    [InlineData("", "BEN", "BEN")]
    [InlineData(null, null, ProjectDataDistributionRules.UnspecifiedMajor)]
    [InlineData("UNDECLARED", null, ProjectDataDistributionRules.UnspecifiedMajor)]
    [InlineData("SE", null, ProjectDataDistributionRules.UnspecifiedMajor)]
    [InlineData("SE", "BIT_AI", ProjectDataDistributionRules.UnspecifiedMajor)]
    public void MajorBucket_UsesEnrollmentMajorThenProfileAndGroupsInvalidCodesAsUnspecified(
        string? enrollment, string? profile, string expected) =>
        ProjectDataMajorBucket.Resolve(enrollment, profile).Should().Be(expected);

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    [InlineData("")]
    public async Task Handler_DeniesRolesOutsideAdminAndLecturerBeforeTouchingData(string role)
    {
        var result = await new GetProjectDataDistributionQueryHandler(_context)
            .HandleAsync(new GetProjectDataRequest(), Guid.NewGuid(), role);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ProjectDataAccessDenied);
        _ = _context.DidNotReceiveWithAnyArgs().Projects;
    }

    [Fact]
    public async Task Handler_RejectsOverlongSearchBeforeTouchingData()
    {
        var result = await new GetProjectDataDistributionQueryHandler(_context)
            .HandleAsync(new GetProjectDataRequest { Search = new string('a', 101) }, Guid.NewGuid(), SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ProjectDataValidationError);
        _ = _context.DidNotReceiveWithAnyArgs().Projects;
    }

    [Theory]
    [InlineData("BIT_SE", true)]
    [InlineData(" bba_mkt ", true)]
    [InlineData("UNSPECIFIED", true)]
    [InlineData("unspecified", true)]
    [InlineData("UNDECLARED", false)]
    [InlineData("SE", false)]
    [InlineData("", false)]
    public void MajorFilterValue_AcceptsOnlyKnownMajorsAndTheUnspecifiedBucket(string value, bool expected) =>
        EHub.Application.Features.ProjectData.Common.ProjectDataQuery.IsMajorFilterValue(value).Should().Be(expected);

    [Theory]
    [InlineData("BIT_SE", true)]
    [InlineData("NOPE", false)]
    [InlineData(null, true)]
    public void ListValidator_ChecksTheMajorFilter(string? major, bool expected) =>
        new EHub.Application.Features.ProjectData.GetProjectData.GetProjectDataRequestValidator()
            .Validate(new GetProjectDataRequest { Major = major }).IsValid.Should().Be(expected);
}
