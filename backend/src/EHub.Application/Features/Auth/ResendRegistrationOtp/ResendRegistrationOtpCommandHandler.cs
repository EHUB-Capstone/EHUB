using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Exceptions;
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
        try
        {
            // Roll back the replacement challenge and resend counter on SMTP failure.
            // Never retry this transaction automatically after an external send.
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                ct => ResendAsync(request, ct), cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            _logger.LogWarning(
                "Registration email resend failed. FailureKind: {FailureKind}; RetryAfterUtc: {RetryAfterUtc}",
                exception.FailureKind, exception.RetryAfterUtc);
            return Result.Failure<RegisterResult>(exception.FailureKind == EmailDeliveryFailureKind.QuotaExceeded
                ? AuthErrors.EmailTemporarilyUnavailable
                : AuthErrors.EmailDeliveryFailed);
        }
        catch (SerializableTransactionConflictException)
        {
            return Result.Failure<RegisterResult>(AuthErrors.VerificationResendTooSoon);
        }
    }

    private async Task<Result<RegisterResult>> ResendAsync(
        ResendRegistrationOtpRequest request,
        CancellationToken cancellationToken)
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (EmailDeliveryException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Registration email provider threw {ExceptionType}", exception.GetType().Name);
            throw new EmailDeliveryException(EmailDeliveryFailureKind.Unknown);
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
