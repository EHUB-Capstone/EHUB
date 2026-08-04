using EHub.Contracts.Subjects;
using EHub.Shared.Results;

namespace EHub.Application.Features.Subjects.ManageSemester;

public interface ICurrentSemesterHandler
{
    Task<Result<CurrentSemesterResponse>> GetAsync(CancellationToken cancellationToken = default);
    Task<Result<CurrentSemesterResponse>> SetAsync(SetCurrentSemesterRequest request, CancellationToken cancellationToken = default);
}
