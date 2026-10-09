using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.ResetPassword;

public sealed class ResetPasswordCommandHandler : IResetPasswordCommandHandler
{
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IPasswordResetTokenService _passwordResetTokenService;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPasswordResetTokenService passwordResetTokenService,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        IDateTimeProvider dateTimeProvider,
        IUnitOfWork unitOfWork)
    {
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _passwordResetTokenService = passwordResetTokenService;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _dateTimeProvider = dateTimeProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> HandleAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var tokenHash = _passwordResetTokenService.HashToken(request.Token);

        var resetToken = await _passwordResetTokenRepository.GetByTokenHashAsync(
            tokenHash,
            cancellationToken);

        if (resetToken is null || resetToken.UsedAt.HasValue || resetToken.ExpiresAt <= utcNow)
        {
            return Result.Failure(AuthErrors.PasswordResetTokenInvalid);
        }

        var user = resetToken.User;

        if (!CanUserResetPassword(user))
        {
            return Result.Failure(AuthErrors.PasswordResetTokenInvalid);
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        resetToken.UsedAt = utcNow;

        // Revoke all active refresh tokens/sessions
        await _refreshTokenRepository.RevokeAllActiveByUserIdAsync(
            user.Id,
            utcNow,
            cancellationToken);

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendPasswordChangedNotificationAsync(
            user.Email,
            user.FullName,
            cancellationToken);

        return Result.Success();
    }

    private static bool CanUserResetPassword(User user)
    {
        return user.Status is UserStatus.Active or UserStatus.PendingApproval;
    }
}
