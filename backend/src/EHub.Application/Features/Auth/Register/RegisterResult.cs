using System;
using EHub.Contracts.Auth;

namespace EHub.Application.Features.Auth.Register;

public sealed class RegisterResult
{
    public string Status { get; init; } = string.Empty;
    public bool RequiresEmailVerification { get; init; }
    public bool RequiresApproval { get; init; }
    public string Message { get; init; } = string.Empty;
    public Guid? RegistrationId { get; init; }
    public string? MaskedEmail { get; init; }
    public DateTime? VerificationExpiresAtUtc { get; init; }
    public DateTime? ResendAvailableAtUtc { get; init; }
    public UserSummaryResponse? User { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
