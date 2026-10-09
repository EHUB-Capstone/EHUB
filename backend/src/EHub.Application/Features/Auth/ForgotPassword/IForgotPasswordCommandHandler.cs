using System.Threading;
using System.Threading.Tasks;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.ForgotPassword;

public interface IForgotPasswordCommandHandler
{
    Task<Result> HandleAsync(
        ForgotPasswordRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
