using EHub.Application.Features.Dashboard.GetAdminDashboard;
using EHub.Contracts.Common;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = SystemPolicies.AdminOnly)]
public sealed class DashboardController(IGetAdminDashboardQueryHandler handler) : ControllerBase
{
    [HttpGet("admin")]
    public async Task<IActionResult> GetAdminDashboard(CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(
                result.Error.Message,
                result.Error.Code));
        }

        return Ok(ApiResponse<object>.SuccessResponse(result.Value!, "Admin dashboard retrieved successfully."));
    }
}
