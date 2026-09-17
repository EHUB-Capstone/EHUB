namespace EHub.Application.Common.Interfaces.Services;

/// <summary>Best-effort notifications that tell authorized clients to refetch class-scoped data.</summary>
public interface IClassRealtimePublisher
{
    Task PublishMajorUpdatedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid classId, Guid studentId, string majorCode, CancellationToken cancellationToken = default);
    Task PublishProposalReviewedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid classId, Guid proposalId, CancellationToken cancellationToken = default);
    Task PublishCheckpointRequirementsUpdatedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid teamId, int checkpointNumber, CancellationToken cancellationToken = default);
    Task PublishCheckpointEvaluationUpdatedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid teamId, int checkpointNumber, CancellationToken cancellationToken = default);
}
