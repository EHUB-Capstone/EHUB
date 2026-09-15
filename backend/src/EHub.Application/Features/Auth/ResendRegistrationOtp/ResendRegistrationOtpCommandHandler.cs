using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Models.Identity;
using EHub.Application.Features.Auth.Register;
using EHub.Contracts.Auth;
using EHub.Domain.Enums;
using EHub.Shared.Results;
using EHub.Shared.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Application.Features.Auth.ResendRegistrationOtp;

public sealed class ResendRegistrationOtpCommandHandler : IResendRegistrationOtpCommandHandler
{
    private readonly IPendingRegistrationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRegistrationOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RegistrationOtpOptions _options;
    private readonly ILogger<ResendRegistrationOtpCommandHandler> _logger;

    public ResendRegistrationOtpCommandHandler(
        IPendingRegistrationRepository repository,
        IUnitOfWork unitOfWork,
        IRegistrationOtpService otpService,
        IEmailService emailService,
        IDateTimeProvider dateTimeProvider,
        IOptions<RegistrationOtpOptions> options,
        ILogger<ResendRegistrationOtpCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _emailService = emailService;
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<RegisterResult>> HandleAsync(
        ResendRegistrationOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var registration = await _repository.GetByIdAsync(
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

        if (registration.FailedAttemptCount >= _options.MaximumAttempts)
        {
            return Result.Failure<RegisterResult>(AuthErrors.VerificationAttemptsExceeded);
        }

        var now = _dateTimeProvider.UtcNow;
        if (registration.LastSentAtUtc.HasValue &&
            registration.LastSentAtUtc.Value.AddSeconds(_options.ResendCooldownSeconds) > now)
        {
            return Result.Failure<RegisterResult>(AuthErrors.VerificationResendTooSoon);
        }

        if (registration.ResendCount >= _options.MaximumResends)
        {
            return Result.Failure<RegisterResult>(AuthErrors.VerificationRateLimited);
        }

        var otp = _otpService.GenerateCode();
        registration.OtpHash = _otpService.HashCode(registration.Id, otp);
        registration.OtpExpiresAtUtc = now.AddMinutes(_options.ExpirationMinutes);
        registration.LastSentAtUtc = now;
        registration.ResendCount++;
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
                "Registration verification email resend failed for registration {RegistrationId}",
                registration.Id);
            return Result.Failure<RegisterResult>(AuthErrors.EmailDeliveryFailed);
        }

        return Result.Success(new RegisterResult
        {
            Status = "PendingEmailVerification",
            RequiresEmailVerification = true,
            Message = "A new verification code has been sent to your email address.",
            RegistrationId = registration.Id,
            MaskedEmail = SensitiveDataMasker.MaskEmail(registration.Email),
            VerificationExpiresAtUtc = registration.OtpExpiresAtUtc,
            ResendAvailableAtUtc = now.AddSeconds(_options.ResendCooldownSeconds)
        });
    }
}
