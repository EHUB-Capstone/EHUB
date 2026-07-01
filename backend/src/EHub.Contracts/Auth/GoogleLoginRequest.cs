namespace EHub.Contracts.Auth;

public sealed class GoogleLoginRequest
{
    public string IdToken { get; init; } = string.Empty;
}
