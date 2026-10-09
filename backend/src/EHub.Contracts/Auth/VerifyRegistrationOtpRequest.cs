namespace EHub.Contracts.Auth;

public sealed class VerifyRegistrationOtpRequest
{
    public Guid RegistrationId { get; init; }
    public string Otp { get; init; } = string.Empty;
}
