using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Auth;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;

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

    public GoogleLoginCommandHandler(
        IGoogleAuthService googleAuthService,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService)
    {
        _googleAuthService = googleAuthService;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify idToken with Google
        var googleResult = await _googleAuthService.VerifyIdTokenAsync(
            request.IdToken,
            cancellationToken);

        if (googleResult.IsFailure)
        {
            return Result.Failure<AuthResponse>(googleResult.Error);
        }

        var googleUser = googleResult.Value;

        // 2. Defensive check: Email verification status from Google
        if (!googleUser.EmailVerified)
        {
            return Result.Failure<AuthResponse>(AuthErrors.GoogleEmailNotVerified);
        }

        var normalizedEmail = googleUser.Email.Trim().ToLowerInvariant();

        // 3. Find registered user in EHUB database
        var user = await _userRepository.GetByEmailWithRolesAsync(
            normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthResponse>(AuthErrors.AccountNotRegistered);
        }

        // 4. Validate user status
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

        if (user.Status == UserStatus.Inactive)
        {
            return Result.Failure<AuthResponse>(AuthErrors.UserInactive);
        }

        if (user.Status != UserStatus.Active)
        {
            return Result.Failure<AuthResponse>(AuthErrors.UserInactive);
        }

        // 5. Get roles
        var roles = user.UserRoles
            .Select(userRole => userRole.Role.Name)
            .ToArray();

        // 6. Generate and save tokens
        var accessToken = _jwtTokenService.GenerateAccessToken(user, roles);
        var refreshToken = _refreshTokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            ExpiresAt = refreshToken.ExpiresAt
        };

        await _refreshTokenRepository.AddAsync(
            refreshTokenEntity,
            cancellationToken);

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

        var response = new AuthResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = refreshToken.RawToken,
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
