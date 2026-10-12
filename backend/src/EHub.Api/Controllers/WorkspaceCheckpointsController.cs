using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Workspaces.GetCheckpointOverview;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Application.Features.Workspaces.CheckpointLinks;
using EHub.Application.Features.Workspaces.CheckpointFeedback;
using EHub.Application.Features.Workspaces.CheckpointEvaluations;
using EHub.Application.Features.Workspaces.CheckpointRequirements;
using EHub.Application.Features.Workspaces.CourseAssessmentEvaluations;
using EHub.Application.Features.Workspaces.EvaluationReportExport;
using EHub.Contracts.Common;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/workspace/checkpoints")]
[Authorize]
public sealed class WorkspaceCheckpointsController(
    ICurrentUserService currentUser,
    ILogger<WorkspaceCheckpointsController> logger) : ControllerBase
{
    [HttpGet("teams/{teamId:guid}")]
    public async Task<IActionResult> GetOverview(
        Guid teamId,
        [FromServices] IGetWorkspaceCheckpointOverviewQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            teamId,
            UserId,
            Role,
            cancellationToken);

        return ToResponse(result);
    }

    [HttpPost("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/uploads")]
    public async Task<IActionResult> InitiateUpload(
        Guid teamId,
        int checkpointNumber,
        [FromBody] InitiateCheckpointFileUploadRequest request,
        [FromServices] ICheckpointFileUploadHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.InitiateAsync(teamId, checkpointNumber, request, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<CheckpointFileUploadSessionResponse>.SuccessResponse(result.Value, "Upload session created."))
            : ToErrorResponse(result.Error);
    }

    [HttpPost("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/uploads/{uploadId:guid}/complete")]
    public async Task<IActionResult> CompleteUpload(
        Guid teamId,
        int checkpointNumber,
        Guid uploadId,
        [FromServices] ICheckpointFileUploadHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.CompleteAsync(teamId, checkpointNumber, uploadId, UserId, Role, cancellationToken);
        return ToFileResponse(result, "File uploaded.");
    }

    [HttpGet("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/files/{fileId:guid}/download")]
    public async Task<IActionResult> DownloadFile(Guid teamId, int checkpointNumber, Guid fileId, [FromServices] ICheckpointFileHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.DownloadAsync(teamId, checkpointNumber, fileId, UserId, Role, cancellationToken);
        if (result.IsSuccess) return File(result.Value.Content, result.Value.ContentType, result.Value.OriginalName);
        return ToErrorResponse(result.Error);
    }

    [HttpGet("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/files/{fileId:guid}/download-url")]
    public async Task<IActionResult> GetDownloadUrl(Guid teamId, int checkpointNumber, Guid fileId, [FromServices] ICheckpointFileHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.GetDownloadUrlAsync(teamId, checkpointNumber, fileId, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<CheckpointFileDownloadUrlResponse>.SuccessResponse(result.Value, "Download link created."))
            : ToErrorResponse(result.Error);
    }

    [HttpGet("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/files/{fileId:guid}/preview-source")]
    public async Task<IActionResult> GetPreviewSource(
        Guid teamId,
        int checkpointNumber,
        Guid fileId,
        [FromServices] ICheckpointFileHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] bool retry = false)
    {
        var result = await handler.GetPreviewSourceAsync(teamId, checkpointNumber, fileId, retry, UserId, Role, cancellationToken);
        if (result.IsFailure) return ToErrorResponse(result.Error);

        // The URL grants read access to one file for 15 minutes; it must never be cached or shared.
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        var body = ApiResponse<CheckpointFilePreviewSourceResponse>.SuccessResponse(result.Value, "Preview source resolved.");
        return result.Value.Status == "Preparing"
            ? StatusCode(StatusCodes.Status202Accepted, body)
            : Ok(body);
    }

    [HttpGet("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/files/{fileId:guid}/preview")]
    public async Task<IActionResult> PreviewFile(
        Guid teamId,
        int checkpointNumber,
        Guid fileId,
        [FromServices] ICheckpointFileHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.PreviewAsync(teamId, checkpointNumber, fileId, UserId, Role, cancellationToken);
        if (result.IsFailure) return ToErrorResponse(result.Error);

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        if (result.Value.Timings is { } timings)
        {
            // Visible in DevTools > Network > Timing; contains no file names or URLs.
            Response.Headers.Append("Server-Timing",
                $"auth;dur={timings.AuthMs}, storage;dur={timings.StorageMs}, convert;dur={timings.ConvertMs}, " +
                $"cache;desc=\"{(result.Value.FromCache ? "hit" : "miss")}\"");
            logger.LogInformation(
                "Preview served for file {FileId}: cache {Cache}, auth {AuthMs} ms, storage {StorageMs} ms, convert {ConvertMs} ms, {Bytes} bytes.",
                fileId, result.Value.FromCache ? "hit" : "miss", timings.AuthMs, timings.StorageMs, timings.ConvertMs, result.Value.Content.Length);
        }

        return File(result.Value.Content, "application/pdf", enableRangeProcessing: true);
    }

    [HttpDelete("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/files/{fileId:guid}")]
    public async Task<IActionResult> DeleteFile(Guid teamId, int checkpointNumber, Guid fileId, [FromServices] ICheckpointFileHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.DeleteAsync(teamId, checkpointNumber, fileId, UserId, Role, cancellationToken);
        if (result.IsSuccess) return Ok(ApiResponse<object>.SuccessResponse(new { }, "File deleted."));
        return ToErrorResponse(result.Error);
    }

    [HttpPost("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/links")]
    public async Task<IActionResult> CreateLink(Guid teamId, int checkpointNumber,
        [FromBody] SaveWorkspaceCheckpointLinkRequest request,
        [FromServices] ICheckpointLinkHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.CreateAsync(teamId, checkpointNumber, request, UserId, Role, cancellationToken);
        return ToLinkResponse(result, "Link submitted.");
    }

    [HttpPut("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/links/{linkId:guid}")]
    public async Task<IActionResult> UpdateLink(Guid teamId, int checkpointNumber, Guid linkId,
        [FromBody] SaveWorkspaceCheckpointLinkRequest request,
        [FromServices] ICheckpointLinkHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.UpdateAsync(teamId, checkpointNumber, linkId, request, UserId, Role, cancellationToken);
        return ToLinkResponse(result, "Submitted link updated.");
    }

    [HttpDelete("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/links/{linkId:guid}")]
    public async Task<IActionResult> DeleteLink(Guid teamId, int checkpointNumber, Guid linkId,
        [FromServices] ICheckpointLinkHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.DeleteAsync(teamId, checkpointNumber, linkId, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<object>.SuccessResponse(new { }, "Submitted link deleted."))
            : ToErrorResponse(result.Error);
    }

    [HttpPut("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/requirements")]
    public async Task<IActionResult> UpdateRequirements(Guid teamId, int checkpointNumber,
        [FromBody] UpdateWorkspaceCheckpointRequirementsRequest request,
        [FromServices] ICheckpointRequirementHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.UpdateAsync(teamId, checkpointNumber, request, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<WorkspaceCheckpointSubmissionResponse>.SuccessResponse(result.Value, "Checkpoint requirements saved."))
            : ToErrorResponse(result.Error);
    }

    [HttpPost("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/feedback")]
    public async Task<IActionResult> AddFeedback(Guid teamId, int checkpointNumber, [FromBody] CreateWorkspaceCheckpointFeedbackRequest request, [FromServices] ICheckpointFeedbackHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.CreateAsync(teamId, checkpointNumber, request, UserId, Role, cancellationToken);
        return ToFeedbackResponse(result);
    }

    [HttpDelete("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/feedback/{feedbackId:guid}")]
    public async Task<IActionResult> DeleteFeedback(Guid teamId, int checkpointNumber, Guid feedbackId, [FromServices] ICheckpointFeedbackHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.DeleteAsync(teamId, checkpointNumber, feedbackId, UserId, Role, cancellationToken);
        return result.IsSuccess ? Ok(ApiResponse<object>.SuccessResponse(new { }, "Feedback deleted.")) : ToErrorResponse(result.Error);
    }

    [HttpGet("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/evaluation-summary")]
    public async Task<IActionResult> GetEvaluationSummary(Guid teamId, int checkpointNumber,
        [FromServices] ICheckpointEvaluationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.GetSummaryAsync(teamId, checkpointNumber, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<WorkspaceCheckpointEvaluationSummaryResponse>.SuccessResponse(result.Value, "Checkpoint evaluation retrieved."))
            : ToErrorResponse(result.Error);
    }

    [HttpGet("classes/{classId:guid}/students/{studentId:guid}/previous-scores")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    public async Task<IActionResult> GetPreviousStudentScores(Guid classId, Guid studentId,
        [FromServices] EHub.Application.Features.Workspaces.StudentPreviousScores.StudentPreviousScoresHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetAsync(classId, studentId, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<StudentPreviousScoresResponse>.SuccessResponse(result.Value, "Previous scores retrieved."))
            : StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
    }

    [HttpPost("evaluation-grading")]
    public async Task<IActionResult> GetEvaluationGradingBatch(
        [FromBody] EvaluationGradingBatchRequest request,
        [FromServices] ICheckpointEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetGradingBatchAsync(request, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<EvaluationGradingBatchResponse>.SuccessResponse(
                result.Value, "Evaluation grading data retrieved."))
            : ToErrorResponse(result.Error);
    }

    [HttpPost("evaluation-grading/export")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    public async Task<IActionResult> ExportEvaluationReport(
        [FromBody] EvaluationReportExportRequest request,
        [FromServices] IEvaluationReportExportHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName)
            : ToErrorResponse(result.Error);
    }

    [HttpPost("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/evaluations")]
    public async Task<IActionResult> SaveEvaluation(Guid teamId, int checkpointNumber,
        [FromBody] SaveWorkspaceCheckpointEvaluationRequest request,
        [FromServices] ICheckpointEvaluationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.SaveAsync(teamId, checkpointNumber, request, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<WorkspaceCheckpointEvaluationResponse>.SuccessResponse(result.Value, "Checkpoint evaluation saved."))
            : ToErrorResponse(result.Error);
    }

    [HttpPut("evaluations/{evaluationId:guid}")]
    public async Task<IActionResult> UpdateEvaluation(Guid evaluationId,
        [FromBody] SaveWorkspaceCheckpointEvaluationRequest request,
        [FromServices] ICheckpointEvaluationHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.UpdateAsync(evaluationId, request, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<WorkspaceCheckpointEvaluationResponse>.SuccessResponse(result.Value, "Checkpoint evaluation updated."))
            : ToErrorResponse(result.Error);
    }

    [HttpPut("evaluations/{evaluationId:guid}/publish")]
    [Authorize(Policy = SystemPolicies.LecturerOnly)]
    public async Task<IActionResult> PublishEvaluation(
        Guid evaluationId,
        [FromServices] ICheckpointEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.PublishAsync(evaluationId, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<WorkspaceEvaluationPublicationResponse>.SuccessResponse(
                result.Value, "Evaluation published."))
            : ToErrorResponse(result.Error);
    }

    [HttpPut("evaluations/{evaluationId:guid}/unpublish")]
    [Authorize(Policy = SystemPolicies.LecturerOnly)]
    public async Task<IActionResult> UnpublishEvaluation(
        Guid evaluationId,
        [FromServices] ICheckpointEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.UnpublishAsync(evaluationId, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<WorkspaceEvaluationUnpublicationResponse>.SuccessResponse(
                result.Value, "Published scores hidden."))
            : ToErrorResponse(result.Error);
    }

    [HttpPut("evaluations/publication/bulk")]
    [Authorize(Policy = SystemPolicies.LecturerOnly)]
    public async Task<IActionResult> UpdateEvaluationPublicationBatch(
        [FromBody] BulkWorkspaceEvaluationPublicationRequest request,
        [FromServices] ICheckpointEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.UpdatePublicationBatchAsync(request, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<BulkWorkspaceEvaluationPublicationResponse>.SuccessResponse(
                result.Value, "Evaluation publication statuses updated."))
            : ToErrorResponse(result.Error);
    }

    [HttpGet("teams/{teamId:guid}/course-assessments")]
    public async Task<IActionResult> GetCourseAssessments(
        Guid teamId,
        [FromServices] ICourseAssessmentEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetAsync(teamId, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<CourseAssessmentEvaluationListResponse>.SuccessResponse(
                result.Value, "Course assessments retrieved."))
            : ToErrorResponse(result.Error);
    }

    [HttpPut("teams/{teamId:guid}/course-assessments/{assessmentId:guid}")]
    public async Task<IActionResult> SaveCourseAssessment(
        Guid teamId,
        Guid assessmentId,
        [FromBody] SaveCourseAssessmentEvaluationRequest request,
        [FromServices] ICourseAssessmentEvaluationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.SaveAsync(teamId, assessmentId, request, UserId, Role, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse<CourseAssessmentEvaluationResponse>.SuccessResponse(
                result.Value, "Course assessment score saved."))
            : ToErrorResponse(result.Error);
    }

    private Guid UserId => currentUser.UserId ?? Guid.Empty;

    private string Role
    {
        get
        {
            if (currentUser.Roles.Any(role => IsRole(role, SystemRoles.Admin)))
            {
                return SystemRoles.Admin;
            }

            if (currentUser.Roles.Any(role => IsRole(role, SystemRoles.Lecturer)))
            {
                return SystemRoles.Lecturer;
            }

            if (currentUser.Roles.Any(role => IsRole(role, SystemRoles.Mentor)))
            {
                return SystemRoles.Mentor;
            }

            if (currentUser.Roles.Any(role => IsRole(role, SystemRoles.Student)))
            {
                return SystemRoles.Student;
            }

            return string.Empty;
        }
    }

    private static IActionResult ToResponse(
        Result<WorkspaceCheckpointOverviewResponse> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(
                ApiResponse<WorkspaceCheckpointOverviewResponse>.SuccessResponse(
                    result.Value,
                    "Workspace checkpoints retrieved."));
        }

        var response = ApiResponse<object>.FailureResponse(
            result.Error.Message,
            result.Error.Code);
        return result.Error.Code == ErrorCodes.WorkspaceAccessDenied
            ? new ObjectResult(response) { StatusCode = StatusCodes.Status403Forbidden }
            : new BadRequestObjectResult(response);
    }

    private static IActionResult ToFileResponse(Result<WorkspaceCheckpointFileResponse> result, string message) =>
        result.IsSuccess
            ? new OkObjectResult(ApiResponse<WorkspaceCheckpointFileResponse>.SuccessResponse(result.Value, message))
            : ToErrorResponse(result.Error);

    private static IActionResult ToLinkResponse(Result<WorkspaceCheckpointLinkResponse> result, string message) =>
        result.IsSuccess
            ? new OkObjectResult(ApiResponse<WorkspaceCheckpointLinkResponse>.SuccessResponse(result.Value, message))
            : ToErrorResponse(result.Error);

    private static IActionResult ToFeedbackResponse(Result<WorkspaceCheckpointFeedbackResponse> result) =>
        result.IsSuccess
            ? new OkObjectResult(ApiResponse<WorkspaceCheckpointFeedbackResponse>.SuccessResponse(result.Value, "Feedback posted."))
            : ToErrorResponse(result.Error);

    private static IActionResult ToErrorResponse(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);
        return error.Code == ErrorCodes.WorkspaceAccessDenied
            ? new ObjectResult(response) { StatusCode = StatusCodes.Status403Forbidden }
            : error.Code is ErrorCodes.CommonNotFoundError or ErrorCodes.WorkspaceNotFound
                ? new NotFoundObjectResult(response)
                : error.Code == ErrorCodes.WorkspaceFilePreviewUnsupported
                    ? new ObjectResult(response) { StatusCode = StatusCodes.Status415UnsupportedMediaType }
                    : error.Code == ErrorCodes.WorkspaceUploadSessionExpired
                        ? new ObjectResult(response) { StatusCode = StatusCodes.Status410Gone }
                    : error.Code == ErrorCodes.WorkspaceUploadObjectMissing
                        ? new ConflictObjectResult(response)
                    : error.Code == ErrorCodes.WorkspaceUploadTooManyPending
                        ? new ObjectResult(response) { StatusCode = StatusCodes.Status429TooManyRequests }
                    : error.Code == ErrorCodes.WorkspaceFilePreviewConversionFailed
                        ? new ObjectResult(response) { StatusCode = StatusCodes.Status422UnprocessableEntity }
                        : error.Code == ErrorCodes.WorkspaceFilePreviewUnavailable
                            ? new ObjectResult(response) { StatusCode = StatusCodes.Status503ServiceUnavailable }
                : new BadRequestObjectResult(response);
    }

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
}
