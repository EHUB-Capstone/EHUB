using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Features.Auth.Common;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.Login;

public interface ILoginCommandHandler
{
    Task<Result<AuthSessionResult>> HandleAsync(
        EmailPasswordLoginRequest request,
        CancellationToken cancellationToken = default);
}
