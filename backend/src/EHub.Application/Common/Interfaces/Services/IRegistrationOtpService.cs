namespace EHub.Application.Common.Interfaces.Services;

public interface IRegistrationOtpService
{
    string GenerateCode();
    string HashCode(Guid registrationId, string code);
    bool VerifyCode(Guid registrationId, string code, string expectedHash);
}
