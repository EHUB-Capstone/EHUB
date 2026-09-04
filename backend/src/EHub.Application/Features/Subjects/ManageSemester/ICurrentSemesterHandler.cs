using EHub.Contracts.Subjects;
using EHub.Shared.Results;

namespace EHub.Application.Features.Subjects.ManageSemester;

public interface ICurrentSemesterHandler
{
    Task<Result<CurrentSemesterResponse>> GetAsync(CancellationToken cancellationToken = default);
    Task<Result<SemesterListResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<ClassCreationSemesterOptionsResponse>> GetClassCreationOptionsAsync(CancellationToken cancellationToken = default);
    Task<Result<SemesterResponse>> PlanAsync(PlanSemesterRequest request, CancellationToken cancellationToken = default);
    Task<Result<SemesterResponse>> UpdateDatesAsync(Guid semesterId, UpdateSemesterDatesRequest request, CancellationToken cancellationToken = default);
    Task<Result<CurrentSemesterResponse>> SetAsync(SetCurrentSemesterRequest request, CancellationToken cancellationToken = default);
    Task<Result<CurrentSemesterResponse>> CorrectAsync(CorrectActiveSemesterRequest request, CancellationToken cancellationToken = default);
    Task<Result<SemesterCompletionPreviewResponse>> PreviewCompletionAsync(Guid semesterId, CancellationToken cancellationToken = default);
    Task<Result<SemesterResponse>> CompleteAsync(Guid semesterId, ChangeSemesterLifecycleRequest request, CancellationToken cancellationToken = default);
    Task<Result<SemesterResponse>> ReopenAsync(Guid semesterId, ChangeSemesterLifecycleRequest request, CancellationToken cancellationToken = default);
}
