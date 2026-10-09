using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Auth.UpdateOwnMajor;
using EHub.Contracts.Auth;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Auth.UpdateOwnMajor;

public sealed class UpdateOwnMajorCommandHandlerTests
{
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IApplicationDbContext _context = Substitute.For<IApplicationDbContext>();
    private readonly IDateTimeProvider _dateTime = Substitute.For<IDateTimeProvider>();

    [Fact]
    public async Task HandleAsync_ReturnsUnauthorized_WhenCallerIsNotAuthenticated()
    {
        var handler = new UpdateOwnMajorCommandHandler(_currentUser, _context, _dateTime);

        var result = await handler.HandleAsync(new UpdateOwnMajorRequest { MajorCode = MajorCodes.BIT_SE });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.CommonUnauthorizedError);
    }

    [Fact]
    public async Task HandleAsync_ReturnsForbidden_WhenCallerIsNotAStudent()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(Guid.NewGuid());
        _currentUser.Roles.Returns([SystemRoles.Lecturer]);
        var handler = new UpdateOwnMajorCommandHandler(_currentUser, _context, _dateTime);

        var result = await handler.HandleAsync(new UpdateOwnMajorRequest { MajorCode = MajorCodes.BIT_SE });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.CommonForbiddenError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UNDECLARED")]
    [InlineData("unknown")]
    public async Task HandleAsync_ReturnsInvalidMajor_BeforeLoadingStudent(string majorCode)
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(Guid.NewGuid());
        _currentUser.Roles.Returns([SystemRoles.Student]);
        var handler = new UpdateOwnMajorCommandHandler(_currentUser, _context, _dateTime);

        var result = await handler.HandleAsync(new UpdateOwnMajorRequest { MajorCode = majorCode });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.AuthInvalidMajor);
    }
}
