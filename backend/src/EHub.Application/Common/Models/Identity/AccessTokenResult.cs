using System;

namespace EHub.Application.Common.Models.Identity;

public sealed class AccessTokenResult
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
