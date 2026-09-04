using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Models.Identity;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using EHub.Shared.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler : IRegisterCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPendingRegistrationRepository _pendingRegistrationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRegistrationOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RegistrationOtpOptions _otpOptions;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPendingRegistrationRepository pendingRegistrationRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IRegistrationOtpService otpService,
        IEmailService emailService,
        IDateTimeProvider dateTimeProvider,
        IOptions<RegistrationOtpOptions> otpOptions,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _pendingRegistrationRepository = pendingRegistrationRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _emailService = emailService;
        _dateTimeProvider = dateTimeProvider;
        _otpOptions = otpOptions.Value;
        _logger = logger;
    }

    public async Task<Result<RegisterResult>> HandleAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            _logger.LogWarning(
                "Registration rejected because the email already belongs to an account. Email: {Email}",
                SensitiveDataMasker.MaskEmail(request.Email));
            return Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists);
        }

        var roleName = request.Role.Trim();
        if (!SystemRoles.PublicRegisterRoles.Contains(roleName) ||
            await _roleRepository.GetByNameAsync(roleName, cancellationToken) is null)
        {
            return Result.Failure<RegisterResult>(AuthErrors.InvalidRole);
        }

        if (roleName == SystemRoles.Student)
        {
            if (string.IsNullOrWhiteSpace(request.MajorCode))
            {
                return Result.Failure<RegisterResult>(AuthErrors.StudentMajorRequired);
            }

            if (!MajorCodes.IsValid(request.MajorCode))
            {
                return Result.Failure<RegisterResult>(AuthErrors.InvalidMajor);
            }
        }

        var now = _dateTimeProvider.UtcNow;
        var registration = await _pendingRegistrationRepository.GetByNormalizedEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (registration is not null)
        {
            if (registration.Status == PendingRegistrationStatus.Completed)
            {
                return Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists);
            }

            var activeChallenge = registration.OtpExpiresAtUtc > now;
            if (activeChallenge && !_passwordHasher.Verify(request.Password, registration.PasswordHash))
            {
                return Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyExists);
            }

            if (activeChallenge && registration.FailedAttemptCount >= _otpOptions.MaximumAttempts)
            {
                return Result.Failure<RegisterResult>(AuthErrors.VerificationAttemptsExceeded);
            }

            if (activeChallenge && registration.LastSentAtUtc.HasValue &&
                registration.LastSentAtUtc.Value.AddSeconds(_otpOptions.ResendCooldownSeconds) > now)
            {
                return Result.Failure<RegisterResult>(AuthErrors.VerificationResendTooSoon);
            }

            if (activeChallenge && registration.ResendCount >= _otpOptions.MaximumResends)
            {
                return Result.Failure<RegisterResult>(AuthErrors.VerificationRateLimited);
            }

            if (!activeChallenge)
            {
                registration.FailedAttemptCount = 0;
                registration.ResendCount = 0;
            }
            else
            {
                registration.ResendCount++;
            }

            registration.FullName = request.FullName.Trim();
            registration.Email = normalizedEmail;
            registration.PasswordHash = _passwordHasher.Hash(request.Password);
            registration.RoleName = roleName;
            registration.MajorCode = roleName == SystemRoles.Student
                ? request.MajorCode!.Trim().ToUpperInvariant()
                : null;
            registration.Status = PendingRegistrationStatus.Pending;
            registration.CompletedAtUtc = null;
            registration.CompletedUserId = null;
        }
        else
        {
            registration = new PendingRegistration
            {
                FullName = request.FullName.Trim(),
                Email = normalizedEmail,
                NormalizedEmail = normalizedEmail,
                PasswordHash = _passwordHasher.Hash(request.Password),
                RoleName = roleName,
                MajorCode = roleName == SystemRoles.Student
                    ? request.MajorCode!.Trim().ToUpperInvariant()
                    : null
            };
            await _pendingRegistrationRepository.AddAsync(registration, cancellationToken);
        }

        var otp = _otpService.GenerateCode();
        registration.OtpHash = _otpService.HashCode(registration.Id, otp);
        registration.OtpExpiresAtUtc = now.AddMinutes(_otpOptions.ExpirationMinutes);
        registration.LastSentAtUtc = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendRegistrationOtpAsync(
                registration.Email,
                registration.FullName,
                otp,
                registration.OtpExpiresAtUtc,
                cancellationToken);
        }
        catch (Exception exception)
        {
            registration.LastSentAtUtc = null;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogError(
                exception,
                "Registration verification email delivery failed for registration {RegistrationId}",
                registration.Id);
            return Result.Failure<RegisterResult>(AuthErrors.EmailDeliveryFailed);
        }

        _logger.LogInformation(
            "Pending registration {RegistrationId} created for role {Role}",
            registration.Id,
            roleName);

        return Result.Success(CreatePendingResult(registration));
    }

    private RegisterResult CreatePendingResult(PendingRegistration registration)
    {
        return new RegisterResult
        {
            Status = "PendingEmailVerification",
            RequiresEmailVerification = true,
            RequiresApproval = false,
            Message = "A verification code has been sent to your email address.",
            RegistrationId = registration.Id,
            MaskedEmail = SensitiveDataMasker.MaskEmail(registration.Email),
            VerificationExpiresAtUtc = registration.OtpExpiresAtUtc,
            ResendAvailableAtUtc = registration.LastSentAtUtc?.AddSeconds(
                _otpOptions.ResendCooldownSeconds)
        };
    }
}
