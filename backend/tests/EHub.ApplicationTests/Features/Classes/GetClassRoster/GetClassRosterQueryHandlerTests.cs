using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.GetClassRoster;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.GetClassRoster;

public sealed class GetClassRosterQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenUserIsMentor_ReturnsAccessDeniedBeforeLoadingClass()
    {
        var context = Substitute.For<IApplicationDbContext>();
        var handler = new GetClassRosterQueryHandler(context);

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            new GetClassRosterRequest(),
            Guid.NewGuid(),
            SystemRoles.Mentor);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }
}
