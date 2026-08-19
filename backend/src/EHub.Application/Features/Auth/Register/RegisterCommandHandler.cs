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
        ILogger<RegisterCommandHandler> logger)
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
        _logger = logger;
    }

    public async Task<Result<RegisterResult>> HandleAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Check duplicate email
        var isEmailDuplicate = await _userRepository.ExistsByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (isEmailDuplicate)
        {
            _logger.LogWarning(
                "Register failed. Reason: email already exists. Email: {Email}.",
                SensitiveDataMasker.MaskEmail(request.Email));
            return Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists);
        }

        // 2. Validate role
        var roleName = request.Role.Trim();
        if (roleName != SystemRoles.Student &&
            roleName != SystemRoles.Lecturer &&
            roleName != SystemRoles.Mentor)
        {
            _logger.LogWarning(
                "Register failed. Reason: invalid role '{Role}'.",
                roleName);
            return Result.Failure<RegisterResult>(AuthErrors.InvalidRole);
        }

        var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
        if (role is null)
        {
            _logger.LogWarning("Register failed. Reason: role '{Role}' not found in database.", roleName);
            return Result.Failure<RegisterResult>(AuthErrors.InvalidRole);
        }

        // 3. Validate Student specific constraints
        if (roleName == SystemRoles.Student)
        {
            if (string.IsNullOrWhiteSpace(request.MajorCode))
            {
                _logger.LogWarning("Register failed. Reason: student major code is required.");
                return Result.Failure<RegisterResult>(AuthErrors.StudentMajorRequired);
            }

            var isValidMajor = MajorCodes.IsValid(request.MajorCode);
            if (!isValidMajor)
            {
                _logger.LogWarning(
                    "Register failed. Reason: invalid student major code '{Major}'.",
                    request.MajorCode);
                return Result.Failure<RegisterResult>(AuthErrors.InvalidMajor);
            }
        }

        // 4. Create user
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Status = roleName == SystemRoles.Student ? UserStatus.Active : UserStatus.PendingApproval
        };

        RegisterResult response = null!;

        await _unitOfWork.ExecuteInTransactionAsync(async (ct) =>
        {
            await _userRepository.AddAsync(user, ct);

            var userRole = new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            };
            await _userRoleRepository.AddAsync(userRole, ct);

            if (roleName == SystemRoles.Student)
            {
                var student = await _studentRepository.GetUnlinkedByEmailAsync(normalizedEmail, ct);
                if (student is null)
                {
                    student = new Student
                    {
                        UserId = user.Id,
                        FullName = user.FullName,
                        Email = user.Email,
                        MajorCode = request.MajorCode!.Trim().ToUpperInvariant()
                    };
                    await _studentRepository.AddAsync(student, ct);
                }
                else
                {
                    // A class import may have created the roster profile before the
                    // student registered. Reuse it so enrollments, teams, and the
                    // account all point at the same Student row.
                    student.UserId = user.Id;
                    student.FullName = user.FullName;
                    student.Email = user.Email;
                    student.MajorCode = request.MajorCode!.Trim().ToUpperInvariant();
                    student.Status = StudentStatus.Active;
                    student.UpdatedAt = DateTime.UtcNow;
                    student.UpdatedBy = user.Id;
                    _studentRepository.Update(student);
                }

                // Auto-login: Generate and save tokens
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

                response = new RegisterResult
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

                response = new RegisterResult
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
