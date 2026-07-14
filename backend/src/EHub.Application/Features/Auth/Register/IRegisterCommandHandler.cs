using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.Register;

public interface IRegisterCommandHandler
{
    Task<Result<RegisterResponse>> HandleAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);
}
