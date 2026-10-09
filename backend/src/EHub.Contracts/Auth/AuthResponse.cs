using System;

namespace EHub.Contracts.Auth;

public sealed class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public UserSummaryResponse User { get; init; } = default!;
}
