namespace EHub.Application.Common.Models.Identity;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public int TokenExpirationMinutes { get; set; } = 15;
}
