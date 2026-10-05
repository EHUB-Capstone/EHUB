using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.GroupProjectConsistency;

public interface IGetGroupProjectConsistencyQueryHandler
{
    Task<Result<GroupProjectConsistencyResponse>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
