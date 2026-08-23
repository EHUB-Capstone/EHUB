namespace EHub.Contracts.Auth;

public sealed class ResendRegistrationOtpRequest
{
    public Guid RegistrationId { get; init; }
}
