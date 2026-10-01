using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Dashboard.GetAcademicOverview;
using EHub.Application.Features.Dashboard.GetAdminDashboard;
using EHub.Contracts.Common;
using EHub.Contracts.Dashboard;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(
    IGetAdminDashboardQueryHandler adminDashboardHandler,
    IGetAcademicOverviewQueryHandler academicOverviewHandler,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("admin")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> GetAdminDashboard(CancellationToken cancellationToken)
    {
        var result = await adminDashboardHandler.HandleAsync(cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(
                result.Error.Message,
                result.Error.Code));
        }

        return Ok(ApiResponse<object>.SuccessResponse(result.Value!, "Admin dashboard retrieved successfully."));
    }

    [HttpGet("academic-overview")]
    [Authorize(Policy = SystemPolicies.LecturerOnly)]
    public async Task<IActionResult> GetAcademicOverview(
        [FromQuery] GetAcademicOverviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Unauthorized(ApiResponse<AcademicOverviewResponse>.FailureResponse(
                "An authenticated lecturer is required.",
                ErrorCodes.CommonUnauthorizedError));
        }

        var result = await academicOverviewHandler.HandleAsync(
            currentUser.UserId.Value,
            request,
            cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == ErrorCodes.ClassAccessDenied)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<AcademicOverviewResponse>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code));
            }

            return BadRequest(ApiResponse<AcademicOverviewResponse>.FailureResponse(
                result.Error.Message,
                result.Error.Code));
        }

        return Ok(ApiResponse<AcademicOverviewResponse>.SuccessResponse(
            result.Value!,
            "Academic overview retrieved successfully."));
    }
}
