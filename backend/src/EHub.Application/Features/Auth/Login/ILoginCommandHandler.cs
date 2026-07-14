using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.Login;

public interface ILoginCommandHandler
{
    Task<Result<AuthResponse>> HandleAsync(
        EmailPasswordLoginRequest request,
        CancellationToken cancellationToken = default);
}
