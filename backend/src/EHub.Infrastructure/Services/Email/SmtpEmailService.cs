using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Services;
using EHub.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EHub.Infrastructure.Services.Email;

public sealed class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var subject = "[EHUB] Reset your password";

        var safeFullName = WebUtility.HtmlEncode(fullName);
        var safeResetUrl = WebUtility.HtmlEncode(resetUrl);

        var htmlBody = $"""
        <div style="font-family: Arial, sans-serif; line-height: 1.6; color: #111827;">
            <h2>Reset your EHUB password</h2>
            <p>Hello {safeFullName},</p>
            <p>We received a request to reset your EHUB account password.</p>
            <p>
                <a href="{safeResetUrl}"
                   style="display:inline-block;padding:10px 16px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;">
                    Reset Password
                </a>
            </p>
            <p>This link will expire at <strong>{expiresAt:yyyy-MM-dd HH:mm:ss} UTC</strong>.</p>
            <p>If you did not request this password reset, you can safely ignore this email.</p>
            <hr />
            <p style="font-size:12px;color:#6b7280;">
                EHUB - Entrepreneurship Hub
            </p>
        </div>
        """;

        var textBody = $"""
        Hello {fullName},

        We received a request to reset your EHUB account password.

        Reset password link:
        {resetUrl}

        This link will expire at {expiresAt:yyyy-MM-dd HH:mm:ss} UTC.

        If you did not request this password reset, you can safely ignore this email.

        EHUB - Entrepreneurship Hub
        """;

        await SendEmailAsync(
            toEmail,
            fullName,
            subject,
            htmlBody,
            textBody,
            cancellationToken);

        _logger.LogInformation(
            "Password reset email sent to {Email}",
            toEmail);
    }

    public async Task SendPasswordChangedNotificationAsync(
        string toEmail,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        var subject = "[EHUB] Your password has been changed";

        var safeFullName = WebUtility.HtmlEncode(fullName);

        var htmlBody = $"""
        <div style="font-family: Arial, sans-serif; line-height: 1.6; color: #111827;">
            <h2>Your EHUB password has been changed</h2>
            <p>Hello {safeFullName},</p>
            <p>Your EHUB account password was changed successfully.</p>
            <p>If you did not perform this action, please contact the EHUB administrator immediately.</p>
            <hr />
            <p style="font-size:12px;color:#6b7280;">
                EHUB - Entrepreneurship Hub
            </p>
        </div>
        """;

        var textBody = $"""
        Hello {fullName},

        Your EHUB account password was changed successfully.

        If you did not perform this action, please contact the EHUB administrator immediately.

        EHUB - Entrepreneurship Hub
        """;

        await SendEmailAsync(
            toEmail,
            fullName,
            subject,
            htmlBody,
            textBody,
            cancellationToken);

        _logger.LogInformation(
            "Password changed notification email sent to {Email}",
            toEmail);
    }

    private async Task SendEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken)
    {
        ValidateOptions();

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(
            _options.FromName,
            _options.FromEmail));

        message.To.Add(new MailboxAddress(
            toName,
            toEmail));

        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var smtpClient = new SmtpClient();

        var secureSocketOptions = ResolveSecureSocketOptions(
            _options.SecureSocketOption);

        await smtpClient.ConnectAsync(
            _options.SmtpHost,
            _options.SmtpPort,
            secureSocketOptions,
            cancellationToken);

        await smtpClient.AuthenticateAsync(
            _options.Username,
            _options.Password,
            cancellationToken);

        await smtpClient.SendAsync(
            message,
            cancellationToken);

        await smtpClient.DisconnectAsync(
            true,
            cancellationToken);
    }

    private static SecureSocketOptions ResolveSecureSocketOptions(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "none" => SecureSocketOptions.None,
            "auto" => SecureSocketOptions.Auto,
            "ssl" => SecureSocketOptions.SslOnConnect,
            "sslonconnect" => SecureSocketOptions.SslOnConnect,
            "starttls" => SecureSocketOptions.StartTls,
            "starttlswhenavailable" => SecureSocketOptions.StartTlsWhenAvailable,
            _ => SecureSocketOptions.StartTls
        };
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("Email FromEmail is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            throw new InvalidOperationException("Email SmtpHost is not configured.");
        }

        if (_options.SmtpPort <= 0)
        {
            throw new InvalidOperationException("Email SmtpPort is invalid.");
        }

        if (string.IsNullOrWhiteSpace(_options.Username))
        {
            throw new InvalidOperationException("Email Username is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException("Email Password is not configured.");
        }
    }
}
