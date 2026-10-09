using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.UpdateProfile;

public interface IUpdateProfileCommandHandler
{
    Task<Result<UpdateProfileResponse>> HandleAsync(
        UpdateProfileCommand command,
        CancellationToken cancellationToken = default);
}
