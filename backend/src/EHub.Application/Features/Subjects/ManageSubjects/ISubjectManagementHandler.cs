using EHub.Contracts.Subjects;
using EHub.Shared.Results;

namespace EHub.Application.Features.Subjects.ManageSubjects;

public interface ISubjectManagementHandler
{
    Task<Result<SubjectListResponse>> GetAsync(string? search, string? status, bool activeOnly, CancellationToken cancellationToken = default);
    Task<Result<SubjectResponse>> CreateAsync(CreateSubjectRequest request, CancellationToken cancellationToken = default);
    Task<Result<SubjectResponse>> UpdateAsync(Guid id, UpdateSubjectRequest request, CancellationToken cancellationToken = default);
    Task<Result<SubjectResponse>> DisableAsync(Guid id, CancellationToken cancellationToken = default);
}
