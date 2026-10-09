using EHub.Contracts.Subjects;
using EHub.Shared.Results;

namespace EHub.Application.Features.Subjects.Curriculum;

public interface IGetSubjectCurriculumQueryHandler
{
    Task<Result<SubjectCurriculumResponse>> GetAsync(
        string subjectCode,
        CancellationToken cancellationToken = default);
}
