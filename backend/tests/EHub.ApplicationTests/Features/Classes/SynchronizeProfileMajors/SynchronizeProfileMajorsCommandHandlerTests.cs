using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.SynchronizeProfileMajors;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Classes.SynchronizeProfileMajors;

public sealed class SynchronizeProfileMajorsCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenStudentRequestsSynchronization_ReturnsAccessDenied()
    {
        var context = Substitute.For<IApplicationDbContext>();
        var handler = new SynchronizeProfileMajorsCommandHandler(context);

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SystemRoles.Student);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }
}
