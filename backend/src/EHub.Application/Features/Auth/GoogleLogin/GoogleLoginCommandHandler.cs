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
using EHub.Shared.Security;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Auth.GoogleLogin;

public class GoogleLoginCommandHandler : IGoogleLoginCommandHandler
{
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<GoogleLoginCommandHandler> _logger;

    public GoogleLoginCommandHandler(
        IGoogleAuthService googleAuthService,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        ILogger<GoogleLoginCommandHandler> logger)
    {
        _googleAuthService = googleAuthService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<Result<AuthSessionResult>> HandleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify idToken with Google
        var googleResult = await _googleAuthService.VerifyIdTokenAsync(
            request.IdToken,
            cancellationToken);

        if (googleResult.IsFailure)
        {
            _logger.LogWarning("Google login failed. Reason: invalid Google token.");
            return Result.Failure<AuthSessionResult>(googleResult.Error);
        }

        var googleUser = googleResult.Value;

        // 2. Defensive check: Email verification status from Google
        if (!googleUser.EmailVerified)
        {
            _logger.LogWarning(
                "Google login failed. Reason: Google email not verified. Email: {Email}.",
                SensitiveDataMasker.MaskEmail(googleUser.Email));
            return Result.Failure<AuthSessionResult>(AuthErrors.GoogleEmailNotVerified);
        }

        var normalizedEmail = googleUser.Email.Trim().ToLowerInvariant();

        // 3. Find registered user in EHUB database
        var user = await _userRepository.GetByEmailWithRolesAsync(
            normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "Google login failed. Reason: account not registered. Email: {Email}.",
                SensitiveDataMasker.MaskEmail(googleUser.Email));
            return Result.Failure<AuthSessionResult>(AuthErrors.AccountNotRegistered);
        }

        if (!user.IsEmailVerified)
        {
            user.IsEmailVerified = true;
            _userRepository.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 4. Validate user status
        if (user.Status == UserStatus.PendingApproval)
        {
            _logger.LogWarning(
                "Google login blocked for pending approval account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.AccountPendingApproval);
        }

        if (user.Status == UserStatus.Rejected)
        {
            _logger.LogWarning(
                "Google login blocked for rejected account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.AccountRejected);
        }

        if (user.Status == UserStatus.Blocked)
        {
            _logger.LogWarning(
                "Google login blocked for blocked account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.UserBlocked);
        }

        if (user.Status == UserStatus.Inactive)
        {
            _logger.LogWarning(
                "Google login blocked for inactive account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.UserInactive);
        }

        if (user.Status != UserStatus.Active)
        {
            _logger.LogWarning(
                "Google login blocked for inactive account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.UserInactive);
        }

        // 5. Get roles
        var roles = user.UserRoles
            .Select(userRole => userRole.Role.Name)
            .ToArray();

        // 6. Generate and save tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(user, roles);
        var refreshToken = _refreshTokenService.GenerateRefreshToken();

        var refreshTokenEntity = new EHub.Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            ExpiresAt = refreshToken.ExpiresAt
        };

        await _refreshTokenRepository.AddAsync(
            refreshTokenEntity,
            cancellationToken);

        user.LastLoginAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Resolve MajorCode for Student
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
            AccessToken = accessToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = refreshToken.RawToken,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
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
            "Google login succeeded for user {UserId}.",
            user.Id);

        return Result.Success(response);
    }
}
