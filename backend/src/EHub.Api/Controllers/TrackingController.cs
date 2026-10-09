using EHub.Application.Features.Tracking;
using EHub.Contracts.Common;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/tracking")]
[Authorize(Policy = SystemPolicies.AdminOnly)]
public sealed class TrackingController(ITrackingQueryHandler handler) : ControllerBase
{
    [HttpGet("auth-stats")]
    public async Task<IActionResult> GetAuthStats([FromQuery] int days = 7, CancellationToken cancellationToken = default)
    {
        var result = await handler.GetAuthStatsAsync(days, cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : Ok(ApiResponse<object>.SuccessResponse(
                result.Value!,
                "Authentication statistics retrieved successfully."));
    }
    [HttpGet("online-users")]
    public async Task<IActionResult> GetOnlineUsers(CancellationToken cancellationToken)
    {
        var result = await handler.GetOnlineUsersAsync(cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(
                result.Error.Message,
                result.Error.Code));
        }

        return Ok(ApiResponse<object>.SuccessResponse(result.Value!, "Online users retrieved successfully."));
    }
}
