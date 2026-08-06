using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.ClassLifecycle;

public interface IClassLifecycleCommandHandler
{
    Task<Result<ClassLifecycleResponse>> ArchiveAsync(
        Guid classId,
        ChangeClassLifecycleRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<Result<ClassLifecycleResponse>> RestoreAsync(
        Guid classId,
        ChangeClassLifecycleRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default);
}
