using EHub.Contracts.Subjects;
using EHub.Shared.Results;

namespace EHub.Application.Features.Subjects.Rubrics;

public interface ISubjectRubricHandler
{
    Task<Result<CourseRubricResponse>> CreateAsync(string subjectCode, SaveRubricRequest request, CancellationToken cancellationToken = default);
    Task<Result<CourseRubricResponse>> UpdateAsync(string subjectCode, Guid rubricId, SaveRubricRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string subjectCode, Guid rubricId, CancellationToken cancellationToken = default);
    Task<Result<RubricCriterionResponse>> CreateCriterionAsync(string subjectCode, Guid rubricId, SaveRubricCriterionRequest request, CancellationToken cancellationToken = default);
    Task<Result<RubricCriterionResponse>> UpdateCriterionAsync(string subjectCode, Guid rubricId, Guid criterionId, SaveRubricCriterionRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteCriterionAsync(string subjectCode, Guid rubricId, Guid criterionId, CancellationToken cancellationToken = default);
}
