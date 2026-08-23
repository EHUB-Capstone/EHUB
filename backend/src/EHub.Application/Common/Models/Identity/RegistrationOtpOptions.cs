namespace EHub.Application.Common.Models.Identity;

public sealed class RegistrationOtpOptions
{
    public const string SectionName = "RegistrationOtp";

    public string HashKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 5;
    public int MaximumAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
    public int MaximumResends { get; set; } = 5;
    public int CleanupRetentionHours { get; set; } = 24;
}
