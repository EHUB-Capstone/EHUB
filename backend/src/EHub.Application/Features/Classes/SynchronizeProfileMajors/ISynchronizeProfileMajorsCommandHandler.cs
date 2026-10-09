using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.SynchronizeProfileMajors;

public interface ISynchronizeProfileMajorsCommandHandler
{
    Task<Result<SynchronizeProfileMajorsResponse>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
