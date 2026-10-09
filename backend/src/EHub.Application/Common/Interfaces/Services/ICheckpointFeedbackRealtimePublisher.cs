using EHub.Contracts.Workspaces;

namespace EHub.Application.Common.Interfaces.Services;

public interface ICheckpointFeedbackRealtimePublisher
{
    Task PublishAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid teamId, WorkspaceCheckpointFeedbackResponse feedback, CancellationToken cancellationToken = default);
    Task PublishDeletedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid teamId, int checkpointNumber, Guid feedbackId, CancellationToken cancellationToken = default);
}
