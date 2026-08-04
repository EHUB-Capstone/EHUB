using EHub.Contracts.Subjects;
using EHub.Shared.Results;

namespace EHub.Application.Features.Subjects.TeachingStaff;

public interface ITeachingStaffQueryHandler
{
    Task<Result<TeachingStaffListResponse>> GetAsync(
        string semester,
        int year,
        CancellationToken cancellationToken = default);
}
