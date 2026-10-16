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

    Task SendClassNotificationAsync(
        string toEmail,
        string fullName,
        string subject,
        string title,
        string message,
        CancellationToken cancellationToken = default);

    Task SendClassNotificationWithActionAsync(
        string toEmail,
        string fullName,
        string subject,
        string title,
        string message,
        string actionLabel,
        string actionUrl,
        CancellationToken cancellationToken = default) =>
        SendClassNotificationAsync(
            toEmail,
            fullName,
            subject,
            title,
            $"{message}\n\n{actionLabel}: {actionUrl}",
            cancellationToken);
}
