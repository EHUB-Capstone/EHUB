using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.GetClassDetail;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.GetClassDetail;

public sealed class GetClassDetailQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenUserIsMentor_ReturnsAccessDeniedBeforeLoadingClass()
    {
        var context = Substitute.For<IApplicationDbContext>();
        var handler = new GetClassDetailQueryHandler(context);

        var result = await handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), SystemRoles.Mentor);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }
}
