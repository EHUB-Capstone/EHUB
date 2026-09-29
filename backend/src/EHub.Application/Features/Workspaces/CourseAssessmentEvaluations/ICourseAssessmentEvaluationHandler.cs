using EHub.Contracts.Workspaces;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CourseAssessmentEvaluations;

public interface ICourseAssessmentEvaluationHandler
{
    Task<Result<CourseAssessmentEvaluationListResponse>> GetAsync(
        Guid teamId, Guid userId, string role, CancellationToken cancellationToken = default);

    Task<Result<CourseAssessmentEvaluationResponse>> SaveAsync(
        Guid teamId, Guid assessmentId, SaveCourseAssessmentEvaluationRequest request,
        Guid userId, string role, CancellationToken cancellationToken = default);
}
