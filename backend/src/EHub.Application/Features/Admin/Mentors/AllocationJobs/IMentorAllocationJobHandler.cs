using EHub.Contracts.Mentors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Admin.Mentors.AllocationJobs;

public interface IMentorAllocationJobHandler
{
    Task<Result<MentorAllocationJobResponse>> StartAsync(PreviewMentorAllocationRequest request, CancellationToken ct);
    Task<Result<MentorAllocationJobResponse>> GetAsync(Guid id, CancellationToken ct);
}

public interface IMentorAllocationJobProcessor
{
    Task<(Guid Id, Guid LeaseId)?> ClaimAsync(CancellationToken ct);
    Task<bool> RenewAsync(Guid id, Guid leaseId, CancellationToken ct);
    Task ProcessAsync(Guid id, Guid leaseId, CancellationToken ct);
}
