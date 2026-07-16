using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Auth.Common;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Auth.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRefreshTokenCommandHandler
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IRefreshTokenService refreshTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _refreshTokenService = refreshTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<AuthSessionResult>> HandleAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        // 1. Hash the raw refresh token
        var tokenHash = _refreshTokenService.Hash(rawRefreshToken);

        // 2. Retrieve the stored refresh token from the database
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        if (storedToken is null)
        {
            _logger.LogWarning("Refresh token failed. Reason: invalid token.");
            return Result.Failure<AuthSessionResult>(AuthErrors.RefreshTokenInvalid);
        }

        // 3. Verify expiration
        if (storedToken.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token expired for user {UserId}.", storedToken.UserId);
            return Result.Failure<AuthSessionResult>(AuthErrors.RefreshTokenExpired);
        }

        // 4. Verify revocation
        if (storedToken.RevokedAt is not null)
        {
            _logger.LogWarning(
                "Refresh token already revoked for user {UserId}. Revoked reason: {Reason}.",
                storedToken.UserId,
                storedToken.ReasonRevoked);
            return Result.Failure<AuthSessionResult>(AuthErrors.RefreshTokenRevoked);
        }

        // 5. Retrieve user with roles
        var user = await _userRepository.GetByIdWithRolesAsync(
            storedToken.UserId,
            cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Refresh token failed. Reason: user not found.");
            return Result.Failure<AuthSessionResult>(AuthErrors.RefreshTokenInvalid);
        }

        // 6. Validate user status
        if (user.Status == UserStatus.PendingApproval)
        {
            _logger.LogWarning(
                "Refresh token blocked for pending approval account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.AccountPendingApproval);
        }

        if (user.Status == UserStatus.Rejected)
        {
            _logger.LogWarning(
                "Refresh token blocked for rejected account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.AccountRejected);
        }

        if (user.Status == UserStatus.Blocked)
        {
            _logger.LogWarning(
                "Refresh token blocked for blocked account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.UserBlocked);
        }

        if (user.Status == UserStatus.Inactive)
        {
            _logger.LogWarning(
                "Refresh token blocked for inactive account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.UserInactive);
        }

        if (user.Status != UserStatus.Active)
        {
            _logger.LogWarning(
                "Refresh token blocked for inactive account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.UserInactive);
        }

        // 8. Generate new tokens
        var roles = user.UserRoles
            .Select(userRole => userRole.Role.Name)
            .ToArray();

        var newAccessToken = _jwtTokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = _refreshTokenService.GenerateRefreshToken();

        // 7. Revoke the old token (Rotation)
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.ReasonRevoked = "Rotated";
        storedToken.ReplacedByTokenHash = newRefreshToken.TokenHash;
        _refreshTokenRepository.Update(storedToken);

        var newRefreshTokenEntity = new EHub.Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = newRefreshToken.TokenHash,
            ExpiresAt = newRefreshToken.ExpiresAt
        };

        await _refreshTokenRepository.AddAsync(
            newRefreshTokenEntity,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 9. Resolve MajorCode for Student
        string? majorCode = null;

        if (roles.Contains(SystemRoles.Student))
        {
            var student = await _studentRepository.GetByUserIdAsync(
                user.Id,
                cancellationToken);

            majorCode = student?.MajorCode;
        }

        var response = new AuthSessionResult
        {
            AccessToken = newAccessToken.Token,
            AccessTokenExpiresAt = newAccessToken.ExpiresAt,
            RefreshToken = newRefreshToken.RawToken,
            RefreshTokenExpiresAt = newRefreshToken.ExpiresAt,
            User = new UserSummaryResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Roles = roles,
                Status = user.Status.ToString(),
                MajorCode = majorCode
            }
        };

        _logger.LogInformation(
            "Refresh token rotation succeeded. User: {UserId}.",
            user.Id);

        return Result.Success(response);
    }
}
