using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Dashboard.GetSubmissionAnalytics;
using EHub.Contracts.Common;
using EHub.Contracts.Dashboard;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/dashboard/submission-analytics")]
[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Lecturer)]
public sealed class SubmissionAnalyticsController(
    ICurrentUserService currentUser, IGetSubmissionAnalyticsQueryHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetSubmissionAnalyticsRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
            return Unauthorized(ApiResponse<SubmissionAnalyticsResponse>.FailureResponse(
                "Authentication is required.", ErrorCodes.CommonUnauthorizedError));
        var result = await handler.HandleAsync(currentUser.UserId.Value, currentUser.Roles, request, cancellationToken);
        if (result.IsFailure)
            return StatusCode(result.Error.Code == ErrorCodes.ClassAccessDenied ? 403 : 400,
                ApiResponse<SubmissionAnalyticsResponse>.FailureResponse(result.Error.Message, result.Error.Code));
        return Ok(ApiResponse<SubmissionAnalyticsResponse>.SuccessResponse(result.Value, "Submission analytics retrieved."));
    }
}
