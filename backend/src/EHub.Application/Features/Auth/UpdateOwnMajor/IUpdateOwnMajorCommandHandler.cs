using EHub.Contracts.Auth;
using EHub.Shared.Results;

namespace EHub.Application.Features.Auth.UpdateOwnMajor;

public interface IUpdateOwnMajorCommandHandler
{
    Task<Result<UpdateOwnMajorResponse>> HandleAsync(
        UpdateOwnMajorRequest request,
        CancellationToken cancellationToken = default);
}
