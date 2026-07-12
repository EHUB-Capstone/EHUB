namespace EHub.Contracts.Auth;

public sealed class RegisterRequest
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? Major { get; init; }
}
