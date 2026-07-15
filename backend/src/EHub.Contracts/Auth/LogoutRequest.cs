namespace EHub.Contracts.Auth;

public sealed class LogoutRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
