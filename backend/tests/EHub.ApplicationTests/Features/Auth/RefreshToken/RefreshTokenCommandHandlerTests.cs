using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using EHub.Application.Features.Auth.RefreshToken;
using EHub.Application.Features.Auth;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Models.Identity;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using EHub.Shared.Errors;
using EHub.Domain.Common;

using Microsoft.Extensions.Logging;

namespace EHub.ApplicationTests.Features.Auth.RefreshToken;

public class RefreshTokenCommandHandlerTests
{
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStudentRepository _studentRepository = Substitute.For<IStudentRepository>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<RefreshTokenCommandHandler> _logger = Substitute.For<ILogger<RefreshTokenCommandHandler>>();

    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _handler = new RefreshTokenCommandHandler(
            _refreshTokenService,
            _refreshTokenRepository,
            _userRepository,
            _studentRepository,
            _jwtTokenService,
            _unitOfWork,
            _logger);
    }

    private static void SetId(BaseEntity entity, Guid id)
    {
        var property = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id));
        property?.SetValue(entity, id);
    }

    [Fact]
    public async Task Should_Fail_When_Token_Not_Found()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "raw-token" };
        var tokenHash = "token-hash";
        
        _refreshTokenService.Hash(request.RefreshToken).Returns(tokenHash);
        _refreshTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns((EHub.Domain.Entities.RefreshToken?)null);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshTokenInvalid, result.Error.Code);
    }

    [Fact]
    public async Task Should_Fail_When_Token_Already_Revoked()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "raw-token" };
        var tokenHash = "token-hash";
        var storedToken = new EHub.Domain.Entities.RefreshToken
        {
            UserId = Guid.NewGuid(),
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _refreshTokenService.Hash(request.RefreshToken).Returns(tokenHash);
        _refreshTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(storedToken);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshTokenRevoked, result.Error.Code);
    }

    [Fact]
    public async Task Should_Fail_When_Token_Expired()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "raw-token" };
        var tokenHash = "token-hash";
        var storedToken = new EHub.Domain.Entities.RefreshToken
        {
            UserId = Guid.NewGuid(),
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            RevokedAt = null
        };

        _refreshTokenService.Hash(request.RefreshToken).Returns(tokenHash);
        _refreshTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(storedToken);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshTokenExpired, result.Error.Code);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "raw-token" };
        var tokenHash = "token-hash";
        var userId = Guid.NewGuid();
        var storedToken = new EHub.Domain.Entities.RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = null
        };

        _refreshTokenService.Hash(request.RefreshToken).Returns(tokenHash);
        _refreshTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthRefreshTokenInvalid, result.Error.Code);
    }

    [Theory]
    [InlineData(UserStatus.PendingApproval, ErrorCodes.AuthAccountPendingApproval)]
    [InlineData(UserStatus.Rejected, ErrorCodes.AuthAccountRejected)]
    [InlineData(UserStatus.Blocked, ErrorCodes.AuthUserBlocked)]
    [InlineData(UserStatus.Inactive, ErrorCodes.AuthUserInactive)]
    public async Task Should_Fail_When_User_Not_Active(UserStatus status, string expectedErrorCode)
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "raw-token" };
        var tokenHash = "token-hash";
        var userId = Guid.NewGuid();
        var storedToken = new EHub.Domain.Entities.RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = null
        };
        var user = new User
        {
            Email = "student@example.com",
            Status = status
        };

        _refreshTokenService.Hash(request.RefreshToken).Returns(tokenHash);
        _refreshTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.Error.Code);
    }

    [Fact]
    public async Task Should_Refresh_Successfully_For_Student_With_Rotation()
    {
        // Arrange
        var request = new RefreshTokenRequest { RefreshToken = "raw-token" };
        var tokenHash = "token-hash";
        var userId = Guid.NewGuid();
        var storedToken = new EHub.Domain.Entities.RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = null
        };
        var studentRole = new Role { Name = SystemRoles.Student };
        var user = new User
        {
            Email = "student@example.com",
            FullName = "Nguyen Student A",
            Status = UserStatus.Active,
            UserRoles = new List<UserRole> { new UserRole { Role = studentRole, UserId = userId } }
        };
        SetId(user, userId);

        var newRawToken = "new-raw-token";
        var newHash = "new-hash";
        var newExpires = DateTime.UtcNow.AddDays(7);
        var genRefreshResult = new RefreshTokenResult
        {
            RawToken = newRawToken,
            TokenHash = newHash,
            ExpiresAt = newExpires
        };

        var newAccessToken = "new-access-token";
        var genAccessResult = new AccessTokenResult
        {
            Token = newAccessToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _refreshTokenService.Hash(request.RefreshToken).Returns(tokenHash);
        _refreshTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(storedToken);
        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        
        _jwtTokenService.GenerateAccessToken(user, Arg.Any<IReadOnlyCollection<string>>()).Returns(genAccessResult);
        _refreshTokenService.GenerateRefreshToken().Returns(genRefreshResult);

        var studentProfile = new Student { UserId = userId, MajorCode = MajorCodes.BIT_SE };
        _studentRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(studentProfile);

        // We mock unit of work transaction execution:
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.Arg<Func<CancellationToken, Task>>();
                if (action != null)
                {
                    await action(CancellationToken.None);
                }
            });

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(newAccessToken, result.Value.AccessToken);
        Assert.Equal(newRawToken, result.Value.RefreshToken);
        Assert.NotNull(result.Value.User);
        Assert.Equal(user.Email, result.Value.User.Email);
        Assert.Equal(MajorCodes.BIT_SE, result.Value.User.MajorCode);
        Assert.Contains(SystemRoles.Student, result.Value.User.Roles);

        Assert.NotNull(storedToken.RevokedAt);
        Assert.Equal(newHash, storedToken.ReplacedByTokenHash);
        Assert.Equal("Rotated", storedToken.ReasonRevoked);

        _refreshTokenRepository.Received(1).Update(storedToken);
        await _refreshTokenRepository.Received(1).AddAsync(Arg.Is<EHub.Domain.Entities.RefreshToken>(rt => 
            rt != null && rt.UserId == userId && rt.TokenHash == newHash && rt.ExpiresAt == newExpires), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
