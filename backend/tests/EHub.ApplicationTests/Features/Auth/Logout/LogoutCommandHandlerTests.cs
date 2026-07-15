using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using EHub.Application.Features.Auth.Logout;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Shared.Results;

namespace EHub.ApplicationTests.Features.Auth.Logout;

public class LogoutCommandHandlerTests
{
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _handler = new LogoutCommandHandler(
            _refreshTokenService,
            _refreshTokenRepository,
            _unitOfWork);
    }

    [Fact]
    public async Task Should_Logout_Successfully_When_Token_Exists_And_Not_Revoked()
    {
        // Arrange
        var request = new LogoutRequest { RefreshToken = "raw-token" };
        var tokenHash = "token-hash";
        var storedToken = new EHub.Domain.Entities.RefreshToken
        {
            UserId = Guid.NewGuid(),
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = null
        };

        _refreshTokenService.Hash(request.RefreshToken).Returns(tokenHash);
        _refreshTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(storedToken);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(storedToken.RevokedAt);
        Assert.Equal("Logged out", storedToken.ReasonRevoked);

        _refreshTokenRepository.Received(1).Update(storedToken);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_Success_Idempotent_When_Token_Not_Found()
    {
        // Arrange
        var request = new LogoutRequest { RefreshToken = "raw-token" };
        var tokenHash = "token-hash";

        _refreshTokenService.Hash(request.RefreshToken).Returns(tokenHash);
        _refreshTokenRepository.GetByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns((EHub.Domain.Entities.RefreshToken?)null);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        _refreshTokenRepository.DidNotReceiveWithAnyArgs().Update(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_Success_Idempotent_When_Token_Already_Revoked()
    {
        // Arrange
        var request = new LogoutRequest { RefreshToken = "raw-token" };
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
        Assert.True(result.IsSuccess);

        _refreshTokenRepository.DidNotReceiveWithAnyArgs().Update(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
