using System;
using System.Threading;
using System.Threading.Tasks;

namespace EHub.Application.Common.Interfaces.Services;

public interface IEmailService
{
    Task SendRegistrationOtpAsync(
        string toEmail,
        string fullName,
        string otp,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken = default);

    Task SendPasswordChangedNotificationAsync(
        string toEmail,
        string fullName,
        CancellationToken cancellationToken = default);
}
