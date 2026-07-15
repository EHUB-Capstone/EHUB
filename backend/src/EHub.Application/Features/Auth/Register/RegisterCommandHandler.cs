using System;
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
using EHub.Shared.Security;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler : IRegisterCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IMentorProfileRepository _mentorProfileRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;

    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IStudentRepository studentRepository,
        IMentorProfileRepository mentorProfileRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        ILogger<RegisterCommandHandler> _logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _studentRepository = studentRepository;
        _mentorProfileRepository = mentorProfileRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        this._logger = _logger;
    }

    public async Task<Result<RegisterResponse>> HandleAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim();
        var normalizedEmail = email.ToLowerInvariant();
        var roleName = request.Role.Trim();

        // Defensive check for allowed public roles
        if (!SystemRoles.PublicRegisterRoles.Contains(roleName))
        {
            return Result.Failure<RegisterResponse>(AuthErrors.InvalidRole);
        }

        // Check if email already exists
        var emailExists = await _userRepository.ExistsByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            _logger.LogWarning(
                "Register duplicate email attempt. Email: {Email}.",
                SensitiveDataMasker.MaskEmail(email));
            return Result.Failure<RegisterResponse>(AuthErrors.EmailAlreadyExists);
        }

        // Get Role entity from database
        var role = await _roleRepository.GetByNameAsync(
            roleName,
            cancellationToken);

        if (role is null)
        {
            return Result.Failure<RegisterResponse>(AuthErrors.InvalidRole);
        }

        // Determine initial user status
        var status = roleName == SystemRoles.Student
            ? UserStatus.Active
            : UserStatus.PendingApproval;

        var passwordHash = _passwordHasher.Hash(request.Password);

        // Build core User entity
        var user = new User
        {
            FullName = fullName,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            Status = status
        };

        var userRole = new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id
        };

        RegisterResponse response = default!;

        // Execute in transaction
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _userRepository.AddAsync(user, ct);
            await _userRoleRepository.AddAsync(userRole, ct);

            if (roleName == SystemRoles.Student)
            {
                var student = new Student
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    MajorCode = request.MajorCode?.Trim(),
                    Status = StudentStatus.Active
                };

                await _studentRepository.AddAsync(student, ct);

                // Auto-login for Student
                var roles = new[] { SystemRoles.Student };
                var accessToken = _jwtTokenService.GenerateAccessToken(user, roles);
                var refreshToken = _refreshTokenService.GenerateRefreshToken();

                var refreshTokenEntity = new EHub.Domain.Entities.RefreshToken
                {
                    UserId = user.Id,
                    TokenHash = refreshToken.TokenHash,
                    ExpiresAt = refreshToken.ExpiresAt
                };

                await _refreshTokenRepository.AddAsync(refreshTokenEntity, ct);

                response = new RegisterResponse
                {
                    Status = UserStatus.Active.ToString(),
                    RequiresApproval = false,
                    Message = "Register successfully",
                    AccessToken = accessToken.Token,
                    RefreshToken = refreshToken.RawToken,
                    ExpiresAt = accessToken.ExpiresAt,
                    User = new UserSummaryResponse
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Roles = roles,
                        Status = UserStatus.Active.ToString(),
                        MajorCode = student.MajorCode
                    }
                };
            }
            else
            {
                if (roleName == SystemRoles.Mentor)
                {
                    var mentorProfile = new MentorProfile
                    {
                        UserId = user.Id,
                        Status = MentorProfileStatus.Active,
                        MaxTeams = 3
                    };

                    await _mentorProfileRepository.AddAsync(mentorProfile, ct);
                }

                response = new RegisterResponse
                {
                    Status = UserStatus.PendingApproval.ToString(),
                    RequiresApproval = true,
                    Message = "Your account has been registered and is pending admin approval.",
                    User = new UserSummaryResponse
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        Roles = new[] { roleName },
                        Status = UserStatus.PendingApproval.ToString(),
                        MajorCode = null
                    }
                };
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        _logger.LogInformation(
            "Register succeeded for user {UserId}. Role: {Role}. Status: {Status}.",
            response.User?.Id,
            roleName,
            response.Status);

        return Result.Success(response);
    }
}
