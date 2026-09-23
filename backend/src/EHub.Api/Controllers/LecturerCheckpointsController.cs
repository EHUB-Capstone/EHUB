using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Checkpoints.LecturerManagement;
using EHub.Contracts.Checkpoints;
using EHub.Contracts.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/lecturer/checkpoints")]
[Authorize(Policy = SystemPolicies.LecturerOnly)]
public sealed class LecturerCheckpointsController(
    ICurrentUserService currentUser,
    ILecturerCheckpointManagementHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] GetLecturerCheckpointsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetAsync(request, UserId, cancellationToken);
        return ToResponse(result, "Lecturer checkpoint overview retrieved.");
    }

    [HttpPut("classes/{classId:guid}/definitions/{checkpointId:guid}/schedule")]
    public async Task<IActionResult> SaveSchedule(
        Guid classId,
        Guid checkpointId,
        [FromBody] SaveClassCheckpointScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.SaveScheduleAsync(classId, checkpointId, request, UserId, cancellationToken);
        return ToResponse(result, "Checkpoint schedule saved.");
    }

    [HttpPut("schedules/bulk")]
    public async Task<IActionResult> SaveBulkSchedule(
        [FromBody] BulkSaveClassCheckpointScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.SaveBulkScheduleAsync(request, UserId, cancellationToken);
        return ToResponse(result, "Checkpoint schedule applied to all selected classes.");
    }

    private Guid UserId => currentUser.UserId ?? Guid.Empty;

    private static IActionResult ToResponse<T>(Result<T> result, string message)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(ApiResponse<T>.SuccessResponse(result.Value, message));
        }

        var response = ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code);
        return result.Error.Code switch
        {
            ErrorCodes.ClassAccessDenied => new ObjectResult(response) { StatusCode = StatusCodes.Status403Forbidden },
            ErrorCodes.CommonNotFoundError => new NotFoundObjectResult(response),
            _ => new BadRequestObjectResult(response)
        };
    }
}
