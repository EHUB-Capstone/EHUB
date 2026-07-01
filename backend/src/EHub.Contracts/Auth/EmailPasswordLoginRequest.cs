namespace EHub.Contracts.Auth;

public sealed class EmailPasswordLoginRequest
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
