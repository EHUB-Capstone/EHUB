using System;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.Services.Email;

public sealed class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendRegistrationOtpAsync(
        string toEmail,
        string fullName,
        string otp,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Registration email dispatch was suppressed by the Console provider. Configure SMTP to receive verification codes. Expires: {ExpiresAtUtc}",
            expiresAtUtc);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "=== [Console Email Dispatch] ===\nTo: {Email} ({FullName})\nSubject: Reset Password Link\nReset URL: {ResetUrl}\nExpires: {ExpiresAt}\n================================",
            toEmail,
            fullName,
            resetUrl,
            expiresAt);

        return Task.CompletedTask;
    }

    public Task SendPasswordChangedNotificationAsync(
        string toEmail,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "=== [Console Email Dispatch] ===\nTo: {Email} ({FullName})\nSubject: Password Changed Successfully\nStatus: Secure\n================================",
            toEmail,
            fullName);

        return Task.CompletedTask;
    }
}
