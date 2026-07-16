using System;
using EHub.Contracts.Auth;

namespace EHub.Application.Features.Auth.Register;

public sealed class RegisterResult
{
    public string Status { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
    public string Message { get; init; } = string.Empty;
    public UserSummaryResponse? User { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
