using EHub.Application.Features.Auth.Register;
using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.VerifyRegistrationOtp;

public interface IVerifyRegistrationOtpCommandHandler
{
    Task<Result<RegisterResult>> HandleAsync(
        VerifyRegistrationOtpRequest request,
        CancellationToken cancellationToken = default);
}
