using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.ClassCompletion;

public interface IClassCompletionCommandHandler
{
    Task<Result<ClassCompletionPreviewResponse>> PreviewAsync(
        Guid classId, Guid currentUserId, string currentUserRole, CancellationToken cancellationToken = default);

    Task<Result<ClassLifecycleResponse>> CompleteAsync(
        Guid classId, ChangeClassLifecycleRequest request, Guid currentUserId, string currentUserRole,
        CancellationToken cancellationToken = default);

    Task<Result<ClassLifecycleResponse>> ReopenAsync(
        Guid classId, ChangeClassLifecycleRequest request, Guid currentUserId, string currentUserRole,
        CancellationToken cancellationToken = default);
}
