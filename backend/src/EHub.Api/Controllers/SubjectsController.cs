using EHub.Application.Features.Subjects.ManageSubjects;
using EHub.Application.Features.Subjects.ManageSemester;
using EHub.Application.Features.Subjects.ManageTeachingStaff;
using EHub.Application.Features.Subjects.TeachingStaff;
using EHub.Application.Features.Subjects.Curriculum;
using EHub.Application.Features.Subjects.Roadmap;
using EHub.Application.Features.Subjects.Rubrics;
using EHub.Contracts.Common;
using EHub.Contracts.Subjects;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/subjects")]
[Authorize(Policy = SystemPolicies.StaffOnly)]
public sealed class SubjectsController : ControllerBase
{
    private readonly ISubjectManagementHandler _subjectHandler;
    private readonly ICurrentSemesterHandler _semesterHandler;
    private readonly ITeachingStaffQueryHandler _teachingStaffHandler;
    private readonly ISemesterTeachingStaffCommandHandler _semesterTeachingStaffHandler;
    private readonly IGetSubjectCurriculumQueryHandler _curriculumHandler;
    private readonly ISynchronizeSubjectCheckpointsHandler _checkpointHandler;
    private readonly ISubjectRoadmapHandler _roadmapHandler;
    private readonly ISubjectRubricHandler _rubricHandler;

    public SubjectsController(
        ISubjectManagementHandler subjectHandler,
        ICurrentSemesterHandler semesterHandler,
        ITeachingStaffQueryHandler teachingStaffHandler,
        ISemesterTeachingStaffCommandHandler semesterTeachingStaffHandler,
        IGetSubjectCurriculumQueryHandler curriculumHandler,
        ISynchronizeSubjectCheckpointsHandler checkpointHandler,
        ISubjectRoadmapHandler roadmapHandler,
        ISubjectRubricHandler rubricHandler)
    {
        _subjectHandler = subjectHandler;
        _semesterHandler = semesterHandler;
        _teachingStaffHandler = teachingStaffHandler;
        _semesterTeachingStaffHandler = semesterTeachingStaffHandler;
        _curriculumHandler = curriculumHandler;
        _checkpointHandler = checkpointHandler;
        _roadmapHandler = roadmapHandler;
        _rubricHandler = rubricHandler;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubjects(
        [FromQuery] string? search,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _subjectHandler.GetAsync(search, status, false, cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : Ok(ApiResponse<SubjectListResponse>.SuccessResponse(result.Value!, "Subjects retrieved successfully."));
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSubjects(CancellationToken cancellationToken)
    {
        var result = await _subjectHandler.GetAsync(null, null, true, cancellationToken);
        return Ok(ApiResponse<SubjectListResponse>.SuccessResponse(result.Value!, "Active subjects retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> CreateSubject(
        [FromBody] CreateSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _subjectHandler.CreateAsync(request, cancellationToken);
        return result.IsFailure
            ? Conflict(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : CreatedAtAction(nameof(GetSubjects), ApiResponse<SubjectResponse>.SuccessResponse(result.Value!, "Subject created successfully."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateSubject(
        Guid id,
        [FromBody] UpdateSubjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _subjectHandler.UpdateAsync(id, request, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : Ok(ApiResponse<SubjectResponse>.SuccessResponse(result.Value!, "Subject updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> DisableSubject(Guid id, CancellationToken cancellationToken)
    {
        var result = await _subjectHandler.DisableAsync(id, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : Ok(ApiResponse<SubjectResponse>.SuccessResponse(result.Value!, "Subject disabled successfully."));
    }

    [HttpGet("current-semester")]
    public async Task<IActionResult> GetCurrentSemester(CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.GetAsync(cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : Ok(ApiResponse<CurrentSemesterResponse>.SuccessResponse(
                result.Value!,
                "Current semester retrieved successfully."));
    }

    [HttpPost("current-semester")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> SetCurrentSemester(
        [FromBody] SetCurrentSemesterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.SetAsync(request, cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<CurrentSemesterResponse>.SuccessResponse(
                result.Value!,
                "Active semester updated successfully."));
    }

    [HttpPost("current-semester/correct")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> CorrectCurrentSemester(
        [FromBody] CorrectActiveSemesterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.CorrectAsync(request, cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<CurrentSemesterResponse>.SuccessResponse(
                result.Value!,
                "Active semester corrected successfully."));
    }

    [HttpGet("semesters")]
    public async Task<IActionResult> GetSemesters(CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.GetAllAsync(cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<SemesterListResponse>.SuccessResponse(result.Value!, "Semesters retrieved successfully."));
    }

    [HttpPost("semesters")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> PlanSemester(
        [FromBody] PlanSemesterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.PlanAsync(request, cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : StatusCode(StatusCodes.Status201Created,
                ApiResponse<SemesterResponse>.SuccessResponse(result.Value!, "Semester planned successfully."));
    }

    [HttpPut("semesters/{id:guid}/dates")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateSemesterDates(
        Guid id,
        [FromBody] UpdateSemesterDatesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.UpdateDatesAsync(id, request, cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<SemesterResponse>.SuccessResponse(result.Value!, "Semester dates updated successfully."));
    }

    [HttpGet("semesters/class-creation-options")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> GetClassCreationSemesterOptions(CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.GetClassCreationOptionsAsync(cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<ClassCreationSemesterOptionsResponse>.SuccessResponse(
                result.Value!, "Class creation semester options retrieved successfully."));
    }

    [HttpGet("semesters/{id:guid}/completion-preview")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> PreviewSemesterCompletion(Guid id, CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.PreviewCompletionAsync(id, cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<SemesterCompletionPreviewResponse>.SuccessResponse(
                result.Value!, "Semester completion preview generated successfully."));
    }

    [HttpPost("semesters/{id:guid}/complete")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> CompleteSemester(
        Guid id,
        [FromBody] ChangeSemesterLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.CompleteAsync(id, request, cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<SemesterResponse>.SuccessResponse(result.Value!, "Semester completed successfully."));
    }

    [HttpPost("semesters/{id:guid}/reopen")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> ReopenSemester(
        Guid id,
        [FromBody] ChangeSemesterLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _semesterHandler.ReopenAsync(id, request, cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<SemesterResponse>.SuccessResponse(result.Value!, "Semester reopened successfully."));
    }

    [HttpGet("teaching-staff")]
    public async Task<IActionResult> GetTeachingStaff(
        [FromQuery] string semester,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        var result = await _teachingStaffHandler.GetAsync(semester, year, cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : Ok(ApiResponse<TeachingStaffListResponse>.SuccessResponse(
                result.Value!,
                "Teaching staff retrieved successfully."));
    }

    [HttpGet("teaching-staff/candidates")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> GetTeachingStaffCandidates(CancellationToken cancellationToken)
    {
        var result = await _teachingStaffHandler.GetCandidatesAsync(cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<TeachingStaffCandidateListResponse>.SuccessResponse(
                result.Value!,
                "Teaching staff candidates retrieved successfully."));
    }

    [HttpPost("teaching-staff")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> AddTeachingStaff(
        [FromBody] AddSemesterTeachingStaffRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _semesterTeachingStaffHandler.AddAsync(request, cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<TeachingStaffResponse>.SuccessResponse(
                    result.Value!,
                    "Teaching staff member added to the semester successfully."));
    }

    [HttpPut("teaching-staff/{assignmentId:guid}")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateTeachingStaff(
        Guid assignmentId,
        [FromBody] UpdateSemesterTeachingStaffRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _semesterTeachingStaffHandler.UpdateAsync(
            assignmentId,
            request,
            cancellationToken);
        return result.IsFailure
            ? ToSemesterErrorResponse(result.Error)
            : Ok(ApiResponse<TeachingStaffResponse>.SuccessResponse(
                result.Value!,
                "Semester teaching staff entry updated successfully."));
    }

    [HttpGet("{subjectCode}")]
    public async Task<IActionResult> GetCurriculum(string subjectCode, CancellationToken cancellationToken)
    {
        var result = await _curriculumHandler.GetAsync(subjectCode, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : Ok(ApiResponse<SubjectCurriculumResponse>.SuccessResponse(
                result.Value!,
                "Subject curriculum retrieved successfully."));
    }

    [HttpGet("{subjectCode}/curriculum")]
    public Task<IActionResult> GetCurriculumByRoute(string subjectCode, CancellationToken cancellationToken) =>
        GetCurriculum(subjectCode, cancellationToken);

    [HttpPut("{subjectCode}/checkpoints")]
    public async Task<IActionResult> SynchronizeCheckpoints(string subjectCode, [FromBody] SaveSubjectCheckpointsRequest request, CancellationToken cancellationToken)
    {
        var result = await _checkpointHandler.SynchronizeAsync(subjectCode, request, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<SubjectCurriculumResponse>.SuccessResponse(
                result.Value!,
                "Subject curriculum synchronized successfully."));
        }

        return result.Error.Code == "NOT_FOUND"
            ? NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
    }

    [HttpPost("{subjectCode}/roadmap")]
    public async Task<IActionResult> CreateRoadmapItem(string subjectCode, [FromBody] SaveRoadmapItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _roadmapHandler.CreateAsync(subjectCode, request, cancellationToken);
        return ToRoadmapResult(result, "Roadmap item created successfully.");
    }

    [HttpPut("{subjectCode}/roadmap/{itemId:guid}")]
    public async Task<IActionResult> UpdateRoadmapItem(string subjectCode, Guid itemId, [FromBody] SaveRoadmapItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _roadmapHandler.UpdateAsync(subjectCode, itemId, request, cancellationToken);
        return ToRoadmapResult(result, "Roadmap item updated successfully.");
    }

    [HttpDelete("{subjectCode}/roadmap/{itemId:guid}")]
    public async Task<IActionResult> DeleteRoadmapItem(string subjectCode, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _roadmapHandler.DeleteAsync(subjectCode, itemId, cancellationToken);
        return result.IsFailure
            ? NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : Ok(ApiResponse<object?>.SuccessResponse(null, "Roadmap item deleted successfully."));
    }

    [HttpPost("{subjectCode}/rubrics")]
    public async Task<IActionResult> CreateRubric(string subjectCode, [FromBody] SaveRubricRequest request, CancellationToken cancellationToken)
    {
        var result = await _rubricHandler.CreateAsync(subjectCode, request, cancellationToken);
        return ToRubricResult(result, "Rubric created successfully.");
    }

    [HttpPut("{subjectCode}/rubrics/{rubricId:guid}")]
    public async Task<IActionResult> UpdateRubric(string subjectCode, Guid rubricId, [FromBody] SaveRubricRequest request, CancellationToken cancellationToken)
    {
        var result = await _rubricHandler.UpdateAsync(subjectCode, rubricId, request, cancellationToken);
        return ToRubricResult(result, "Rubric updated successfully.");
    }

    [HttpDelete("{subjectCode}/rubrics/{rubricId:guid}")]
    public async Task<IActionResult> DeleteRubric(string subjectCode, Guid rubricId, CancellationToken cancellationToken)
    {
        var result = await _rubricHandler.DeleteAsync(subjectCode, rubricId, cancellationToken);
        if (result.IsFailure) return NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
        return Ok(ApiResponse<object?>.SuccessResponse(null, "Rubric deleted successfully."));
    }

    [HttpPost("{subjectCode}/rubrics/{rubricId:guid}/criteria")]
    public async Task<IActionResult> CreateCriterion(string subjectCode, Guid rubricId, [FromBody] SaveRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        var result = await _rubricHandler.CreateCriterionAsync(subjectCode, rubricId, request, cancellationToken);
        return ToCriterionResult(result, "Criterion created successfully.");
    }

    [HttpPut("{subjectCode}/rubrics/{rubricId:guid}/criteria/{criterionId:guid}")]
    public async Task<IActionResult> UpdateCriterion(string subjectCode, Guid rubricId, Guid criterionId, [FromBody] SaveRubricCriterionRequest request, CancellationToken cancellationToken)
    {
        var result = await _rubricHandler.UpdateCriterionAsync(subjectCode, rubricId, criterionId, request, cancellationToken);
        return ToCriterionResult(result, "Criterion updated successfully.");
    }

    [HttpDelete("{subjectCode}/rubrics/{rubricId:guid}/criteria/{criterionId:guid}")]
    public async Task<IActionResult> DeleteCriterion(string subjectCode, Guid rubricId, Guid criterionId, CancellationToken cancellationToken)
    {
        var result = await _rubricHandler.DeleteCriterionAsync(subjectCode, rubricId, criterionId, cancellationToken);
        if (result.IsFailure) return NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
        return Ok(ApiResponse<object?>.SuccessResponse(null, "Criterion deleted successfully."));
    }

    private IActionResult ToRoadmapResult(
        EHub.Shared.Results.Result<RoadmapItemResponse> result,
        string successMessage)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<RoadmapItemResponse>.SuccessResponse(result.Value!, successMessage));
        }

        return result.Error.Code == "NOT_FOUND"
            ? NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
    }

    private IActionResult ToRubricResult(
        EHub.Shared.Results.Result<CourseRubricResponse> result,
        string successMessage)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<CourseRubricResponse>.SuccessResponse(result.Value!, successMessage));
        }

        return result.Error.Code == "NOT_FOUND"
            ? NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
    }

    private IActionResult ToCriterionResult(
        EHub.Shared.Results.Result<RubricCriterionResponse> result,
        string successMessage)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<RubricCriterionResponse>.SuccessResponse(result.Value!, successMessage));
        }

        return result.Error.Code == "NOT_FOUND"
            ? NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
    }

    private IActionResult ToSemesterErrorResponse(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);
        return error.Code switch
        {
            ErrorCodes.ClassAccessDenied => StatusCode(StatusCodes.Status403Forbidden, response),
            ErrorCodes.SemesterNotFound => NotFound(response),
            ErrorCodes.SemesterStaffNotFound => NotFound(response),
            ErrorCodes.SemesterConcurrencyConflict or
            ErrorCodes.SemesterActivationBlocked or
            ErrorCodes.SemesterCompletionBlocked or
            ErrorCodes.SemesterInvalidState => Conflict(response),
            ErrorCodes.SemesterStaffConflict or
            ErrorCodes.SemesterStaffInUse => Conflict(response),
            _ => BadRequest(response),
        };
    }

}
