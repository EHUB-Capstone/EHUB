namespace EHub.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; init; } = "Console";

    public string FromName { get; init; } = "EHUB";

    public string FromEmail { get; init; } = string.Empty;

    public string SmtpHost { get; init; } = string.Empty;

    public int SmtpPort { get; init; } = 587;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string SecureSocketOption { get; init; } = "StartTls";

    // Covers connection, authentication and send together; no automatic send retry.
    public int SmtpTimeoutSeconds { get; init; } = 10;

    // Process-local retry delay after explicit provider quota rejection, not quota reset time.
    public int QuotaCooldownMinutes { get; init; } = 15;

    // Opt-in: credentials are supplied through User Secrets or deployment secrets.
    public bool EnableBrevoFallback { get; init; }
    public EmailOptions? Brevo { get; init; }
}
