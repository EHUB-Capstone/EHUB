using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Auth.ChangePassword;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Shared.Errors;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Auth.ChangePassword;

public class ChangePasswordCommandHandlerTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        _handler = new ChangePasswordCommandHandler(
            _currentUserService,
            _userRepository,
            _passwordHasher,
            _unitOfWork);
    }

    [Fact]
    public async Task Should_Fail_When_User_Is_Not_Authenticated()
    {
        _currentUserService.IsAuthenticated.Returns(false);

        var result = await _handler.HandleAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.CommonUnauthorizedError, result.Error.Code);
    }

    [Fact]
    public async Task Should_Fail_When_CurrentPassword_Is_Invalid()
    {
        var user = AuthenticatedUser();
        _passwordHasher.Verify("OldPassword123", user.PasswordHash).Returns(false);

        var result = await _handler.HandleAsync(ValidRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.AuthCurrentPasswordInvalid, result.Error.Code);
        _userRepository.DidNotReceive().Update(Arg.Any<User>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Update_Password_When_CurrentPassword_Is_Valid()
    {
        var user = AuthenticatedUser();
        _passwordHasher.Verify("OldPassword123", user.PasswordHash).Returns(true);
        _passwordHasher.Hash("NewPassword123").Returns("new-hash");

        var result = await _handler.HandleAsync(ValidRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal("new-hash", user.PasswordHash);
        _userRepository.Received(1).Update(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private User AuthenticatedUser()
    {
        var user = new User
        {
            Email = "user@example.com",
            FullName = "Test User",
            PasswordHash = "old-hash"
        };

        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns(user.Id);
        _userRepository.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private static ChangePasswordRequest ValidRequest() => new()
    {
        CurrentPassword = "OldPassword123",
        NewPassword = "NewPassword123",
        ConfirmPassword = "NewPassword123"
    };
}
