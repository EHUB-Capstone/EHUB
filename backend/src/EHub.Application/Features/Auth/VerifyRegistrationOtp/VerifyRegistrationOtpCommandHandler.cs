using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Models.Identity;
using EHub.Application.Features.Auth.Register;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Application.Features.Auth.VerifyRegistrationOtp;

public sealed class VerifyRegistrationOtpCommandHandler : IVerifyRegistrationOtpCommandHandler
{
    private readonly IPendingRegistrationRepository _pendingRegistrationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IMentorProfileRepository _mentorProfileRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRegistrationOtpService _otpService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RegistrationOtpOptions _otpOptions;
    private readonly ILogger<VerifyRegistrationOtpCommandHandler> _logger;

    public VerifyRegistrationOtpCommandHandler(
        IPendingRegistrationRepository pendingRegistrationRepository,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IStudentRepository studentRepository,
        IMentorProfileRepository mentorProfileRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IRegistrationOtpService otpService,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IDateTimeProvider dateTimeProvider,
        IOptions<RegistrationOtpOptions> otpOptions,
        ILogger<VerifyRegistrationOtpCommandHandler> logger)
    {
        _pendingRegistrationRepository = pendingRegistrationRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _studentRepository = studentRepository;
        _mentorProfileRepository = mentorProfileRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _dateTimeProvider = dateTimeProvider;
        _otpOptions = otpOptions.Value;
        _logger = logger;
    }

    public async Task<Result<RegisterResult>> HandleAsync(
        VerifyRegistrationOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _unitOfWork.ExecuteInSerializableTransactionAsync(
                async ct => await VerifyAndCreateAccountAsync(request, ct),
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Registration {RegistrationId} was verified and completed for user {UserId}",
                    request.RegistrationId,
                    result.Value.User?.Id);
            }

            return result;
        }
        catch (SerializableTransactionConflictException)
        {
            _logger.LogWarning(
                "Concurrent verification conflict for registration {RegistrationId}",
                request.RegistrationId);
            return Result.Failure<RegisterResult>(AuthErrors.RegistrationAlreadyCompleted);
        }
    }

    private async Task<Result<RegisterResult>> VerifyAndCreateAccountAsync(
        VerifyRegistrationOtpRequest request,
        CancellationToken cancellationToken)
    {
        var registration = await _pendingRegistrationRepository.GetByIdAsync(
            request.RegistrationId,
            cancellationToken);
        if (registration is null || registration.Status == PendingRegistrationStatus.Cancelled)
        {
            return Result.Failure<RegisterResult>(AuthErrors.RegistrationNotFound);
        }

        if (registration.Status == PendingRegistrationStatus.Completed)
        {
            return Result.Failure<RegisterResult>(AuthErrors.RegistrationAlreadyCompleted);
        }

        var now = _dateTimeProvider.UtcNow;
        if (registration.FailedAttemptCount >= _otpOptions.MaximumAttempts)
        {
            return Result.Failure<RegisterResult>(AuthErrors.VerificationAttemptsExceeded);
        }

        if (registration.OtpExpiresAtUtc <= now)
        {
            return Result.Failure<RegisterResult>(AuthErrors.VerificationCodeExpired);
        }

        if (!_otpService.VerifyCode(registration.Id, request.Otp, registration.OtpHash))
        {
            registration.FailedAttemptCount++;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return registration.FailedAttemptCount >= _otpOptions.MaximumAttempts
                ? Result.Failure<RegisterResult>(AuthErrors.VerificationAttemptsExceeded)
                : Result.Failure<RegisterResult>(AuthErrors.VerificationCodeInvalid);
        }

        if (await _userRepository.ExistsByEmailAsync(
                registration.NormalizedEmail,
                cancellationToken))
        {
            return Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists);
        }

        var role = await _roleRepository.GetByNameAsync(
            registration.RoleName,
            cancellationToken);
        if (role is null)
        {
            return Result.Failure<RegisterResult>(AuthErrors.InvalidRole);
        }

        var user = new User
        {
            FullName = registration.FullName,
            Email = registration.Email,
            NormalizedEmail = registration.NormalizedEmail,
            PasswordHash = registration.PasswordHash,
            IsEmailVerified = true,
            Status = registration.RoleName == SystemRoles.Student
                ? UserStatus.Active
                : UserStatus.PendingApproval
        };
        await _userRepository.AddAsync(user, cancellationToken);
        await _userRoleRepository.AddAsync(
            new UserRole { UserId = user.Id, RoleId = role.Id },
            cancellationToken);

        Student? student = null;
        if (registration.RoleName == SystemRoles.Student)
        {
            student = await _studentRepository.GetUnlinkedByEmailAsync(
                registration.NormalizedEmail,
                cancellationToken);
            if (student is null)
            {
                student = new Student
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    MajorCode = registration.MajorCode!
                };
                await _studentRepository.AddAsync(student, cancellationToken);
            }
            else
            {
                student.UserId = user.Id;
                student.FullName = user.FullName;
                student.Email = user.Email;
                student.MajorCode = registration.MajorCode!;
                student.Status = StudentStatus.Active;
                student.UpdatedAt = now;
                student.UpdatedBy = user.Id;
                _studentRepository.Update(student);
            }
        }
        else if (registration.RoleName == SystemRoles.Mentor)
        {
            await _mentorProfileRepository.AddAsync(
                new MentorProfile
                {
                    UserId = user.Id,
                    Status = MentorProfileStatus.Active,
                    MaxTeams = 3
                },
                cancellationToken);
        }

        string? accessToken = null;
        string? rawRefreshToken = null;
        DateTimeOffset? expiresAt = null;
        if (registration.RoleName == SystemRoles.Student)
        {
            var generatedAccessToken = _jwtTokenService.GenerateAccessToken(
                user,
                new[] { SystemRoles.Student });
            var generatedRefreshToken = _refreshTokenService.GenerateRefreshToken();
            accessToken = generatedAccessToken.Token;
            expiresAt = generatedAccessToken.ExpiresAt;
            rawRefreshToken = generatedRefreshToken.RawToken;
            await _refreshTokenRepository.AddAsync(
                new EHub.Domain.Entities.RefreshToken
                {
                    UserId = user.Id,
                    TokenHash = generatedRefreshToken.TokenHash,
                    ExpiresAt = generatedRefreshToken.ExpiresAt
                },
                cancellationToken);
        }

        registration.Status = PendingRegistrationStatus.Completed;
        registration.CompletedAtUtc = now;
        registration.CompletedUserId = user.Id;
        registration.OtpHash = string.Empty;
        registration.PasswordHash = string.Empty;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requiresApproval = registration.RoleName != SystemRoles.Student;
        return Result.Success(new RegisterResult
        {
            Status = user.Status.ToString(),
            RequiresEmailVerification = false,
            RequiresApproval = requiresApproval,
            Message = requiresApproval
                ? "Your email has been verified. Your account is pending admin approval."
                : "Your email has been verified and your account is ready.",
            User = new UserSummaryResponse
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Roles = new[] { registration.RoleName },
                Status = user.Status.ToString(),
                MajorCode = student?.MajorCode
            },
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresAt = expiresAt
        });
    }
}
