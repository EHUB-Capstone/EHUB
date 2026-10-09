using System;

namespace EHub.Application.Common.Models.Identity;

public sealed class RefreshTokenResult
{
    public string RawToken { get; init; } = string.Empty;
    public string TokenHash { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
