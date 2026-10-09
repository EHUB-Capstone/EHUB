using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.GetCurrentUser;

public interface IGetCurrentUserQueryHandler
{
    Task<Result<CurrentUserResponse>> HandleAsync(
        CancellationToken cancellationToken = default);
}
