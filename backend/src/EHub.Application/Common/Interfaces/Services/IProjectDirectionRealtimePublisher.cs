using EHub.Contracts.Teams;

namespace EHub.Application.Common.Interfaces.Services;

public interface IProjectDirectionRealtimePublisher
{
    Task PublishAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        string eventType,
        Guid classId,
        Guid teamId,
        ProjectDirectionDto direction,
        CancellationToken cancellationToken = default);

    Task PublishNotificationReadyAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        Guid classId,
        Guid teamId,
        CancellationToken cancellationToken = default);
}
