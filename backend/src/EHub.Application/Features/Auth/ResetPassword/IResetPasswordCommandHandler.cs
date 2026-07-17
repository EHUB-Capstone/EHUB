using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.ResetPassword;

public interface IResetPasswordCommandHandler
{
    Task<Result> HandleAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
