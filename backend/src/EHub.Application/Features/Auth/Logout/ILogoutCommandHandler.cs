using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.Logout;

public interface ILogoutCommandHandler
{
    Task<Result> HandleAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default);
}
