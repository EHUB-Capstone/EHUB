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

    public LoginCommandHandler(
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<Result<AuthResponse>> HandleAsync(
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
            return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials);
        }

        // 3. Verify password hash
        var isPasswordValid = _passwordHasher.Verify(
            request.Password,
            user.PasswordHash);

        if (!isPasswordValid)
        {
            return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials);
        }

        // 4. Validate status (only Active allowed to log in)
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
