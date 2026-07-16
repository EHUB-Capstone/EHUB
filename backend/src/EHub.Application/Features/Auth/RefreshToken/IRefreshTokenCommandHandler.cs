using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Features.Auth.Common;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.RefreshToken;

public interface IRefreshTokenCommandHandler
{
    Task<Result<AuthSessionResult>> HandleAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default);
}
