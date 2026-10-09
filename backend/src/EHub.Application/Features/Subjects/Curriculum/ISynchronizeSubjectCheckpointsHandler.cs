using EHub.Contracts.Subjects;
using EHub.Shared.Results;

namespace EHub.Application.Features.Subjects.Curriculum;

public interface ISynchronizeSubjectCheckpointsHandler
{
    Task<Result<SubjectCurriculumResponse>> SynchronizeAsync(
        string subjectCode,
        SaveSubjectCheckpointsRequest request,
        CancellationToken cancellationToken = default);
}
