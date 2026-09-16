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
}
