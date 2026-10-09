namespace EHub.Application.Common.Models.Identity;

public sealed class GoogleUserInfo
{
    public string Subject { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
}
