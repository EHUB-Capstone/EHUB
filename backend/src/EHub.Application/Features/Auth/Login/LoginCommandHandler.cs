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

namespace EHub.Application.Features.Auth.Login;

public sealed class LoginCommandHandler : ILoginCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<Result<AuthSessionResult>> HandleAsync(
        EmailPasswordLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Retrieve user with roles
        var user = await _userRepository.GetByEmailWithRolesAsync(
            normalizedEmail,
            cancellationToken);

        // 2. Defensive check: Email not found
        if (user is null)
        {
            _logger.LogWarning(
                "Login failed. Reason: invalid credentials. Email: {Email}.",
                SensitiveDataMasker.MaskEmail(request.Email));
            return Result.Failure<AuthSessionResult>(AuthErrors.InvalidCredentials);
        }

        // 3. Verify password hash
        var isPasswordValid = _passwordHasher.Verify(
            request.Password,
            user.PasswordHash);

        if (!isPasswordValid)
        {
            _logger.LogWarning(
                "Login failed. Reason: invalid credentials. Email: {Email}.",
                SensitiveDataMasker.MaskEmail(request.Email));
            return Result.Failure<AuthSessionResult>(AuthErrors.InvalidCredentials);
        }

        // 4. Validate status (only Active allowed to log in)
        if (user.Status == UserStatus.PendingApproval)
        {
            _logger.LogWarning(
                "Login blocked for pending approval account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.AccountPendingApproval);
        }

        if (user.Status == UserStatus.Rejected)
        {
            _logger.LogWarning(
                "Login blocked for rejected account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.AccountRejected);
        }

        if (user.Status == UserStatus.Blocked)
        {
            _logger.LogWarning(
                "Login blocked for blocked account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.UserBlocked);
        }

        if (user.Status == UserStatus.Inactive)
        {
            _logger.LogWarning(
                "Login blocked for inactive account. UserId: {UserId}.",
                user.Id);
            return Result.Failure<AuthSessionResult>(AuthErrors.UserInactive);
        }

        if (user.Status != UserStatus.Active)
        {
            _logger.LogWarning(
                "Login blocked for inactive account. UserId: {UserId}.",
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. Resolve major code for Student
        string? majorCode = null;

        if (roles.Contains(SystemRoles.Student))
        {
            var student = await _studentRepository.GetByUserIdAsync(
                user.Id,
                cancellationToken);

            majorCode = student?.MajorCode;
        }

        var result = new AuthSessionResult
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
            "Login succeeded for user {UserId}. Roles: {Roles}.",
            user.Id,
            roles);

        return Result.Success(result);
    }
}
