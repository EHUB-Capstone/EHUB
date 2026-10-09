using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Models.Identity;
using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Identity;

public interface IGoogleAuthService
{
    Task<Result<GoogleUserInfo>> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default);
}
