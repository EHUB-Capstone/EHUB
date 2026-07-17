using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using EHub.Application.Features.Auth.ResetPassword;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;

namespace EHub.ApplicationTests.Features.Auth.ResetPassword;

public class ResetPasswordCommandHandlerTests
{
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IPasswordResetTokenService _passwordResetTokenService = Substitute.For<IPasswordResetTokenService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        _handler = new ResetPasswordCommandHandler(
            _passwordResetTokenRepository,
            _passwordResetTokenService,
            _userRepository,
            _refreshTokenRepository,
            _passwordHasher,
            _emailService,
            _dateTimeProvider,
            _unitOfWork);
    }

    [Fact]
    public async Task Should_Fail_When_Token_Not_Found()
    {
        // Arrange
        var request = new ResetPasswordRequest { Token = "invalid-token", NewPassword = "NewPassword123" };
        _passwordResetTokenService.HashToken(request.Token).Returns("hashed-token");
        _passwordResetTokenRepository.GetByTokenHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns((PasswordResetToken?)null);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthPasswordResetTokenInvalid, result.Error.Code);
    }

    [Fact]
    public async Task Should_Fail_When_Token_Already_Used()
    {
        // Arrange
        var request = new ResetPasswordRequest { Token = "used-token", NewPassword = "NewPassword123" };
        var token = new PasswordResetToken
        {
            TokenHash = "hashed-token",
            UsedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _passwordResetTokenService.HashToken(request.Token).Returns("hashed-token");
        _passwordResetTokenRepository.GetByTokenHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(token);
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthPasswordResetTokenInvalid, result.Error.Code);
    }

    [Fact]
    public async Task Should_Fail_When_Token_Expired()
    {
        // Arrange
        var request = new ResetPasswordRequest { Token = "expired-token", NewPassword = "NewPassword123" };
        var token = new PasswordResetToken
        {
            TokenHash = "hashed-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _passwordResetTokenService.HashToken(request.Token).Returns("hashed-token");
        _passwordResetTokenRepository.GetByTokenHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(token);
        _dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthPasswordResetTokenInvalid, result.Error.Code);
    }

    [Fact]
    public async Task Should_Reset_Password_And_Revoke_RefreshTokens_When_Token_Is_Valid()
    {
        // Arrange
        var request = new ResetPasswordRequest { Token = "valid-token", NewPassword = "NewPassword123" };
        var user = new User
        {
            FullName = "John Doe",
            Email = "john@example.com",
            Status = UserStatus.Active,
            PasswordHash = "OldHash"
        };
        var token = new PasswordResetToken
        {
            TokenHash = "hashed-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            User = user,
            UserId = Guid.NewGuid()
        };

        _passwordResetTokenService.HashToken(request.Token).Returns("hashed-token");
        _passwordResetTokenRepository.GetByTokenHashAsync("hashed-token", Arg.Any<CancellationToken>())
            .Returns(token);
        
        var now = DateTime.UtcNow;
        _dateTimeProvider.UtcNow.Returns(now);
        _passwordHasher.Hash(request.NewPassword).Returns("NewHash");

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("NewHash", user.PasswordHash);
        Assert.Equal(now, token.UsedAt);
        await _refreshTokenRepository.Received(1).RevokeAllActiveByUserIdAsync(user.Id, now, Arg.Any<CancellationToken>());
        _userRepository.Received(1).Update(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendPasswordChangedNotificationAsync(user.Email, user.FullName, Arg.Any<CancellationToken>());
    }
}
