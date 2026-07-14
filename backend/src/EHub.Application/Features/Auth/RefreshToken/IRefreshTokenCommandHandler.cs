using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.RefreshToken;

public interface IRefreshTokenCommandHandler
{
    Task<Result<AuthResponse>> HandleAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);
}
