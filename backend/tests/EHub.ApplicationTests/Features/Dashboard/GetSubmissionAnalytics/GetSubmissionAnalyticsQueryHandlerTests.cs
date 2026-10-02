using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Dashboard.GetSubmissionAnalytics;
using EHub.Contracts.Dashboard;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Dashboard.GetSubmissionAnalytics;

public sealed class GetSubmissionAnalyticsQueryHandlerTests
{
    private readonly GetSubmissionAnalyticsQueryHandler _handler = new(
        Substitute.For<IApplicationDbContext>(), Substitute.For<IDateTimeProvider>(), new GetSubmissionAnalyticsRequestValidator());

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    public async Task RejectsOtherRolesBeforeQueryingData(string role)
    {
        var result = await _handler.HandleAsync(Guid.NewGuid(), [role], new());
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task RejectsMissingIdentity()
    {
        var result = await _handler.HandleAsync(Guid.Empty, [SystemRoles.Admin], new());
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task RejectsInvalidFiltersBeforeQueryingData()
    {
        foreach (var request in new GetSubmissionAnalyticsRequest[] {
            new() { Semester = "XX" }, new() { Year = 1999 }, new() { CheckpointNumber = 0 },
            new() { ClassId = Guid.Empty }, new() { TeamId = Guid.Empty } })
        {
            var result = await _handler.HandleAsync(Guid.NewGuid(), [SystemRoles.Lecturer], request);
            result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        }
    }
}
