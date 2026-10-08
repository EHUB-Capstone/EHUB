using EHub.Contracts.Subjects;
using EHub.Shared.Results;

namespace EHub.Application.Features.Subjects.ManageTeachingStaff;

public interface ISemesterTeachingStaffCommandHandler
{
    Task<Result<TeachingStaffResponse>> AddAsync(
        AddSemesterTeachingStaffRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AddSemesterTeachingStaffBatchResponse>> AddBatchAsync(
        AddSemesterTeachingStaffBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TeachingStaffResponse>> UpdateAsync(
        Guid assignmentId,
        UpdateSemesterTeachingStaffRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<MentorCarryoverPreviewResponse>> PreviewMentorCarryoverAsync(
        PreviewMentorCarryoverRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<MentorCarryoverCommitResponse>> CommitMentorCarryoverAsync(
        CommitMentorCarryoverRequest request,
        CancellationToken cancellationToken = default);
}
