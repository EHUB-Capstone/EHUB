using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Exceptions;
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
    private readonly ISmtpClientFactory _clientFactory;
    private readonly SmtpProviderCooldown _providerCooldown;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SmtpEmailService(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailService> logger,
        ISmtpClientFactory clientFactory,
        SmtpProviderCooldown providerCooldown,
        IDateTimeProvider dateTimeProvider)
    {
        _options = options.Value;
        _logger = logger;
        _clientFactory = clientFactory;
        _providerCooldown = providerCooldown;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task SendRegistrationOtpAsync(
        string toEmail,
        string fullName,
        string otp,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var safeFullName = WebUtility.HtmlEncode(fullName);
        var safeOtp = WebUtility.HtmlEncode(otp);
        var subject = "[EHUB] Verify your email address";
        var htmlBody = $"""
        <div style="font-family:Arial,sans-serif;line-height:1.6;color:#111827;max-width:560px;margin:auto">
            <h2>Verify your EHUB email</h2>
            <p>Hello {safeFullName},</p>
            <p>Use the following one-time code to complete your registration:</p>
            <div style="font-size:32px;font-weight:700;letter-spacing:8px;padding:16px 20px;background:#f8fafc;border-radius:10px;text-align:center">
                {safeOtp}
            </div>
            <p>This code expires at <strong>{expiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC</strong> and can be used only once.</p>
            <p>If you did not request this registration, you can ignore this email.</p>
            <hr />
            <p style="font-size:12px;color:#6b7280">EHUB - Entrepreneurship Hub</p>
        </div>
        """;
        var textBody = $"""
        Hello {fullName},

        Your EHUB registration verification code is: {otp}

        This code expires at {expiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC and can be used only once.
        If you did not request this registration, you can ignore this email.

        EHUB - Entrepreneurship Hub
        """;

        await SendEmailAsync(
            toEmail,
            fullName,
            subject,
            htmlBody,
            textBody,
            cancellationToken);

        _logger.LogInformation("Registration verification email sent");
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

        _logger.LogInformation("Password reset email sent");
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

        _logger.LogInformation("Password changed notification email sent");
    }

    public async Task SendClassNotificationAsync(
        string toEmail,
        string fullName,
        string subject,
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeMessage = WebUtility.HtmlEncode(message);
        var htmlBody = $"""
        <div style="font-family:Arial,sans-serif;line-height:1.6;color:#111827;max-width:560px;margin:auto">
            <h2>{safeTitle}</h2>
            <p style="white-space:pre-line">{safeMessage}</p>
            <hr />
            <p style="font-size:12px;color:#6b7280">EHUB - Entrepreneurship Hub</p>
        </div>
        """;
        var textBody = $"""
        {title}

        {message}

        EHUB - Entrepreneurship Hub
        """;

        await SendEmailAsync(toEmail, fullName, subject, htmlBody, textBody, cancellationToken);
        _logger.LogInformation("Class notification email sent");
    }

    private async Task SendEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken)
    {
        try
        {
            await SendViaProviderAsync(toEmail, toName, subject, htmlBody, textBody,
                _options, _providerCooldown, cancellationToken);
        }
        catch (EmailDeliveryException exception) when (
            exception.FailureKind == EmailDeliveryFailureKind.QuotaExceeded
            && _options.EnableBrevoFallback)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var brevo = _options.Brevo;
            if (brevo is null
                || !string.Equals(brevo.SmtpHost, "smtp-relay.brevo.com", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(brevo.SecureSocketOption, "StartTls", StringComparison.OrdinalIgnoreCase))
            {
                throw new EmailDeliveryException(EmailDeliveryFailureKind.Configuration);
            }

            _logger.LogInformation("Primary SMTP quota exhausted; attempting Brevo fallback");
            // Reuse the same content/OTP. Never fall back on ambiguous transport failures.
            await SendViaProviderAsync(toEmail, toName, subject, htmlBody, textBody,
                brevo, null, cancellationToken);
        }
    }

    private async Task SendViaProviderAsync(
        string toEmail, string toName, string subject, string htmlBody, string textBody,
        EmailOptions options, SmtpProviderCooldown? cooldown, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(options);
        cooldown?.ThrowIfActive(_dateTimeProvider.UtcNow);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.SmtpTimeoutSeconds));

        ISmtpClient? smtpClient = null;
        var accepted = false;
        var stage = EmailDeliveryFailureKind.Configuration;
        var stageTimer = Stopwatch.StartNew();
        try
        {
            using var message = new MimeMessage();
            message.From.Add(new MailboxAddress(options.FromName, options.FromEmail));
            stage = EmailDeliveryFailureKind.RecipientRejected;
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

            stage = EmailDeliveryFailureKind.Configuration;
            smtpClient = _clientFactory.Create();
            smtpClient.Timeout = options.SmtpTimeoutSeconds * 1000;

            stage = EmailDeliveryFailureKind.Connection;
            stageTimer.Restart();
            await smtpClient.ConnectAsync(
                options.SmtpHost,
                options.SmtpPort,
                ResolveSecureSocketOptions(options.SecureSocketOption),
                timeout.Token);
            LogStageDuration("connect", options, stageTimer);

            stage = EmailDeliveryFailureKind.Authentication;
            stageTimer.Restart();
            await smtpClient.AuthenticateAsync(options.Username, options.Password, timeout.Token);
            LogStageDuration("authenticate", options, stageTimer);

            stage = EmailDeliveryFailureKind.ProviderRejected;
            stageTimer.Restart();
            await smtpClient.SendAsync(message, timeout.Token);
            accepted = true;
            LogStageDuration("send", options, stageTimer);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failureKind = ClassifyFailure(exception, stage, options);
            _logger.LogWarning("SMTP stage failed: {Stage}; category {FailureKind}; elapsed {ElapsedMs} ms",
                stage, failureKind, stageTimer.ElapsedMilliseconds);
            DateTime? retryAfterUtc = failureKind == EmailDeliveryFailureKind.QuotaExceeded
                ? cooldown?.RecordQuotaFailure(
                    _dateTimeProvider.UtcNow, TimeSpan.FromMinutes(options.QuotaCooldownMinutes))
                : null;

            // Never retain the provider message or inner exception: they may contain private data.
            throw new EmailDeliveryException(failureKind, retryAfterUtc);
        }
        finally
        {
            if (smtpClient is not null)
            {
                if (accepted)
                {
                    try
                    {
                        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        // DATA acceptance already confirms delivery to the SMTP server.
                        // Close locally without a QUIT round trip on the HTTP critical path.
                        await smtpClient.DisconnectAsync(false, cleanupTimeout.Token);
                    }
                    catch (Exception)
                    {
                        // SMTP already accepted the message. Reporting failure here would cause duplicates.
                        _logger.LogWarning("SMTP disconnect failed after message acceptance; email remains accepted");
                    }
                }

                try
                {
                    smtpClient.Dispose();
                }
                catch (Exception)
                {
                    _logger.LogWarning("SMTP client cleanup failed; delivery outcome is unchanged");
                }
            }
        }
    }

    private void LogStageDuration(string stage, EmailOptions options, Stopwatch timer)
    {
        var provider = string.Equals(options.SmtpHost, "smtp-relay.brevo.com", StringComparison.OrdinalIgnoreCase)
            ? "Brevo" : "Primary";
        _logger.LogInformation("SMTP {Provider} {Stage} completed in {ElapsedMs} ms",
            provider, stage, timer.ElapsedMilliseconds);
    }

    private static EmailDeliveryFailureKind ClassifyFailure(Exception exception, EmailDeliveryFailureKind stage, EmailOptions options)
    {
        if (exception is SmtpCommandException command)
        {
            var isGmail = string.Equals(options.SmtpHost, "smtp.gmail.com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(options.SmtpHost, "smtp-relay.gmail.com", StringComparison.OrdinalIgnoreCase);
            if (isGmail
                && command.Message.Contains("5.4.5", StringComparison.Ordinal)
                && command.Message.Contains("Daily user sending limit exceeded", StringComparison.OrdinalIgnoreCase))
            {
                return EmailDeliveryFailureKind.QuotaExceeded;
            }

            return command.ErrorCode switch
            {
                SmtpErrorCode.SenderNotAccepted => EmailDeliveryFailureKind.SenderRejected,
                SmtpErrorCode.RecipientNotAccepted => EmailDeliveryFailureKind.RecipientRejected,
                _ => stage == EmailDeliveryFailureKind.Authentication
                    ? EmailDeliveryFailureKind.Authentication
                    : EmailDeliveryFailureKind.ProviderRejected
            };
        }

        return exception switch
        {
            SslHandshakeException or System.Security.Authentication.AuthenticationException => EmailDeliveryFailureKind.Tls,
            MailKit.Security.AuthenticationException or SaslException => EmailDeliveryFailureKind.Authentication,
            OperationCanceledException or TimeoutException => EmailDeliveryFailureKind.Timeout,
            SocketException or IOException or MailKit.ProtocolException => EmailDeliveryFailureKind.Connection,
            FormatException or ArgumentException => stage,
            _ => EmailDeliveryFailureKind.Unknown
        };
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

    private static void ValidateOptions(EmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.FromEmail)
            || string.IsNullOrWhiteSpace(options.SmtpHost)
            || options.SmtpPort is < 1 or > 65535
            || string.IsNullOrWhiteSpace(options.Username)
            || string.IsNullOrWhiteSpace(options.Password)
            || options.SmtpTimeoutSeconds is < 1 or > 120
            || options.QuotaCooldownMinutes is < 1 or > 1440)
        {
            throw new EmailDeliveryException(EmailDeliveryFailureKind.Configuration);
        }
    }
}
