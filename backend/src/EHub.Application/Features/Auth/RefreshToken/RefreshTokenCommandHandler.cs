using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRefreshTokenCommandHandler
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IRefreshTokenService refreshTokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenService = refreshTokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Hash the raw refresh token
        var tokenHash = _refreshTokenService.Hash(request.RefreshToken);

        // 2. Retrieve the stored refresh token from the database
        var storedToken = await _refreshTokenRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        if (storedToken is null)
        {
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenInvalid);
        }

        // 3. Check if the token is already revoked
        if (storedToken.RevokedAt is not null)
        {
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenRevoked);
        }

        // 4. Check if the token is expired
        var now = DateTime.UtcNow;
        if (storedToken.ExpiresAt <= now)
        {
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenExpired);
        }

        // 5. Retrieve the user with their roles
        var user = await _userRepository.GetByIdWithRolesAsync(
            storedToken.UserId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthResponse>(AuthErrors.RefreshTokenInvalid);
        }

        // 6. Check user status
        if (user.Status == UserStatus.PendingApproval)
        {
            return Result.Failure<AuthResponse>(AuthErrors.AccountPendingApproval);
        }

        if (user.Status == UserStatus.Rejected)
        {
            return Result.Failure<AuthResponse>(AuthErrors.AccountRejected);
        }

        if (user.Status == UserStatus.Blocked)
        {
            return Result.Failure<AuthResponse>(AuthErrors.UserBlocked);
        }

        if (user.Status == UserStatus.Inactive || user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthResponse>(AuthErrors.UserInactive);
        }

        // 7. Get roles
        var roles = user.UserRoles
            .Select(userRole => userRole.Role.Name)
            .ToArray();

        // 8. Generate new access token & new refresh token (Rotation)
        var accessToken = _jwtTokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = _refreshTokenService.GenerateRefreshToken();

        var newRefreshTokenEntity = new EHub.Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = newRefreshToken.TokenHash,
            ExpiresAt = newRefreshToken.ExpiresAt,
            CreatedAt = now
        };

        // 9. Persist changes in a single transaction (revoke old & add new)
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            storedToken.RevokedAt = now;
            storedToken.ReplacedByTokenHash = newRefreshToken.TokenHash;
            storedToken.ReasonRevoked = "Rotated";
            
            _refreshTokenRepository.Update(storedToken);

            await _refreshTokenRepository.AddAsync(newRefreshTokenEntity, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        // 10. Load student MajorCode if Student role
        string? majorCode = null;
        if (roles.Contains(SystemRoles.Student))
        {
            var student = await _studentRepository.GetByUserIdAsync(
                user.Id,
                cancellationToken);
            
            majorCode = student?.MajorCode;
        }

        var response = new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = newRefreshToken.RawToken,
            ExpiresAt = accessToken.ExpiresAt,
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

        return Result.Success(response);
    }
}
