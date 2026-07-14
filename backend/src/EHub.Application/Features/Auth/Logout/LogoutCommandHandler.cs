using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler : ILogoutCommandHandler
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(
        IRefreshTokenService refreshTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenService = refreshTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Hash the raw refresh token
        var tokenHash = _refreshTokenService.Hash(request.RefreshToken);

        // 2. Retrieve the stored refresh token
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        // 3. If token exists and is not already revoked, revoke it
        if (storedToken is not null && storedToken.RevokedAt is null)
        {
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.ReasonRevoked = "Logged out";
            
            _refreshTokenRepository.Update(storedToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 4. Return success (idempotent behavior)
        return Result.Success();
    }
}
