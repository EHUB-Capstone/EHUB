using EHub.Contracts.Classes;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.CheckpointDeadlines;

public interface IClassCheckpointDeadlineHandler
{
    Task<Result<IReadOnlyCollection<ClassCheckpointDeadlineClassResponse>>> GetAvailableClassesAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<ClassCheckpointDeadlineResponse>>> GetAsync(Guid classId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ClassCheckpointDeadlineResponse>> SaveAsync(Guid classId, int checkpointNumber, SaveClassCheckpointDeadlineRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ClassCheckpointDeadlineBulkResponse>> SaveForClassesAsync(int checkpointNumber, SaveClassCheckpointDeadlineBulkRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
}
