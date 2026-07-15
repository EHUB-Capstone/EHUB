using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.GoogleLogin;

public interface IGoogleLoginCommandHandler
{
    Task<Result<AuthResponse>> HandleAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default);
}
