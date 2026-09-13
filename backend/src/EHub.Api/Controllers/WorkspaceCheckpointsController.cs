using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Workspaces.GetCheckpointOverview;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Application.Features.Workspaces.CheckpointFeedback;
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
    ICurrentUserService currentUser) : ControllerBase
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

    [HttpPost("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/upload")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(
        Guid teamId,
        int checkpointNumber,
        IFormFile file,
        [FromServices] ICheckpointFileHandler handler,
        CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(ApiResponse<object>.FailureResponse("A file is required.", ErrorCodes.WorkspaceValidationError));
        await using var stream = file.OpenReadStream();
        var result = await handler.UploadAsync(teamId, checkpointNumber, stream, file.FileName, file.ContentType, file.Length, UserId, Role, cancellationToken);
        return ToFileResponse(result, "File uploaded.");
    }

    [HttpGet("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/files/{fileId:guid}/download")]
    public async Task<IActionResult> DownloadFile(Guid teamId, int checkpointNumber, Guid fileId, [FromServices] ICheckpointFileHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.DownloadAsync(teamId, checkpointNumber, fileId, UserId, Role, cancellationToken);
        if (result.IsSuccess) return File(result.Value.Content, result.Value.ContentType, result.Value.OriginalName);
        return ToErrorResponse(result.Error);
    }

    [HttpDelete("teams/{teamId:guid}/checkpoints/{checkpointNumber:int}/files/{fileId:guid}")]
    public async Task<IActionResult> DeleteFile(Guid teamId, int checkpointNumber, Guid fileId, [FromServices] ICheckpointFileHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.DeleteAsync(teamId, checkpointNumber, fileId, UserId, Role, cancellationToken);
        if (result.IsSuccess) return Ok(ApiResponse<object>.SuccessResponse(new { }, "File deleted."));
        return ToErrorResponse(result.Error);
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
                : new BadRequestObjectResult(response);
    }

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
}
