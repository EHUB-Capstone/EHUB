using EHub.Contracts.Checkpoints;
using EHub.Shared.Results;

namespace EHub.Application.Features.Checkpoints.LecturerManagement;

public interface ILecturerCheckpointManagementHandler
{
    Task<Result<LecturerCheckpointOverviewResponse>> GetAsync(
        GetLecturerCheckpointsRequest request,
        Guid lecturerId,
        CancellationToken cancellationToken = default);

    Task<Result<ClassCheckpointScheduleResponse>> SaveScheduleAsync(
        Guid classId,
        Guid checkpointId,
        SaveClassCheckpointScheduleRequest request,
        Guid lecturerId,
        CancellationToken cancellationToken = default);

    Task<Result<BulkClassCheckpointScheduleResponse>> SaveBulkScheduleAsync(
        BulkSaveClassCheckpointScheduleRequest request,
        Guid lecturerId,
        CancellationToken cancellationToken = default);
}
