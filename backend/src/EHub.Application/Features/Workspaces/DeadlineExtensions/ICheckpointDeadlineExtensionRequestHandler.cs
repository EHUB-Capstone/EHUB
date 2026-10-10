using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.DeadlineExtensions;

public interface ICheckpointDeadlineExtensionRequestHandler
{
    Task<Result<CheckpointDeadlineExtensionRequestResponse>> GetMineAsync(
        Guid teamId,
        int checkpointNumber,
        DateTime deadlineUtc,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);

    Task<Result<CheckpointDeadlineExtensionRequestResponse>> CreateAsync(
        Guid teamId,
        int checkpointNumber,
        CreateCheckpointDeadlineExtensionRequest request,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);
}
