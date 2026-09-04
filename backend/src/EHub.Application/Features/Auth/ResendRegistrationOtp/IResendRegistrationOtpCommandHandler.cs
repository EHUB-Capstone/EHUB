using EHub.Application.Features.Auth.Register;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.ResendRegistrationOtp;

public interface IResendRegistrationOtpCommandHandler
{
    Task<Result<RegisterResult>> HandleAsync(
        ResendRegistrationOtpRequest request,
        CancellationToken cancellationToken = default);
}
