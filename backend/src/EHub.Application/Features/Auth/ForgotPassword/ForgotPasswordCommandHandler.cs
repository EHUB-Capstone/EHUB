using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Application.Common.Models.Identity;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.ForgotPassword;

public sealed class ForgotPasswordCommandHandler : IForgotPasswordCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IPasswordResetTokenService _passwordResetTokenService;
    private readonly IEmailService _emailService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FrontendOptions _frontendOptions;
    private readonly PasswordResetOptions _passwordResetOptions;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPasswordResetTokenService passwordResetTokenService,
        IEmailService emailService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork,
        IOptions<FrontendOptions> frontendOptions,
        IOptions<PasswordResetOptions> passwordResetOptions)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _passwordResetTokenService = passwordResetTokenService;
        _emailService = emailService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
        _frontendOptions = frontendOptions.Value;
        _passwordResetOptions = passwordResetOptions.Value;
    }

    public async Task<Result> HandleAsync(
        ForgotPasswordRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.GetByEmailWithRolesAsync(
            normalizedEmail,
            cancellationToken);

        // Security: Always return success to prevent email enumeration
        if (user is null)
        {
            return Result.Success();
        }

        if (!CanUserRequestPasswordReset(user))
        {
            return Result.Success();
        }

        var utcNow = _dateTimeProvider.UtcNow;

        // Invalidate older tokens
        await _passwordResetTokenRepository.MarkActiveTokensAsUsedByUserIdAsync(
            user.Id,
            utcNow,
            cancellationToken);

        var rawToken = _passwordResetTokenService.GenerateRawToken();
        var tokenHash = _passwordResetTokenService.HashToken(rawToken);
        var expiresAt = utcNow.AddMinutes(_passwordResetOptions.TokenExpirationMinutes);

        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            RequestedIpAddress = ipAddress,
            RequestedUserAgent = userAgent
        };

        await _passwordResetTokenRepository.AddAsync(resetToken, cancellationToken);

        var resetUrl = $"{_frontendOptions.BaseUrl}/reset-password?token={rawToken}";

        await _emailService.SendPasswordResetEmailAsync(
            user.Email,
            user.FullName,
            resetUrl,
            expiresAt,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static bool CanUserRequestPasswordReset(User user)
    {
        return user.IsEmailVerified &&
            (user.Status is UserStatus.Active or UserStatus.PendingApproval);
    }
}
