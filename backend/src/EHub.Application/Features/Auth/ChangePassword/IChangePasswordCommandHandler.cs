using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.ChangePassword;

public interface IChangePasswordCommandHandler
{
    Task<Result> HandleAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}
