using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.SetEnrollmentMajorLock;

public interface ISetEnrollmentMajorLockCommandHandler
{
    Task<Result<EnrollmentMajorLockResponse>> HandleAsync(
        Guid classId,
        bool shouldLock,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
