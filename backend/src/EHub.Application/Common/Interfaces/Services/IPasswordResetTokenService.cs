namespace EHub.Application.Common.Interfaces.Services;

public interface IPasswordResetTokenService
{
    string GenerateRawToken();
    string HashToken(string rawToken);
}
