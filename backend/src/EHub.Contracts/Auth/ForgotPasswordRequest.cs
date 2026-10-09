namespace EHub.Contracts.Auth;

public sealed class ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}
