using System.Threading;
using System.Threading.Tasks;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.Logout;

public interface ILogoutCommandHandler
{
    Task<Result> HandleAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default);
}
