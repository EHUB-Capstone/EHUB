using EHub.Application.Common.Models.Identity;

namespace EHub.Application.Common.Interfaces.Identity;

public interface IRefreshTokenService
{
    RefreshTokenResult GenerateRefreshToken();
    string Hash(string rawToken);
    bool Verify(string rawToken, string tokenHash);
}
