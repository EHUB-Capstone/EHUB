using System;
using EHub.Contracts.Auth;

namespace EHub.Application.Features.Auth.Common;

public sealed class AuthSessionResult
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTimeOffset AccessTokenExpiresAt { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
    public DateTimeOffset RefreshTokenExpiresAt { get; init; }
    public UserSummaryResponse User { get; init; } = default!;
}
