using EHub.Application.Features.StartupIndustries.ManageStartupIndustries;
using EHub.Contracts.Common;
using EHub.Contracts.StartupIndustries;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/startup-industries")]
[Authorize]
public sealed class StartupIndustriesController(IStartupIndustryManagementHandler handler) : ControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> GetActiveOptions(CancellationToken cancellationToken)
    {
        var result = await handler.GetActiveOptionsAsync(cancellationToken);
        return result.IsFailure
            ? ToErrorResponse(result.Error)
            : Ok(ApiResponse<StartupIndustryListResponse>.SuccessResponse(
                result.Value!, "Active startup industries retrieved successfully."));
    }

    [HttpGet]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> GetIndustries(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetAsync(search, status, sort, cancellationToken);
        return result.IsFailure
            ? ToErrorResponse(result.Error)
            : Ok(ApiResponse<StartupIndustryListResponse>.SuccessResponse(
                result.Value!, "Startup industries retrieved successfully."));
    }

    [HttpPost]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> CreateIndustry(
        [FromBody] CreateStartupIndustryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.CreateAsync(request, cancellationToken);
        return result.IsFailure
            ? ToErrorResponse(result.Error)
            : StatusCode(StatusCodes.Status201Created,
                ApiResponse<StartupIndustryResponse>.SuccessResponse(
                    result.Value!, "Startup industry created successfully."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateIndustry(
        Guid id,
        [FromBody] UpdateStartupIndustryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.UpdateAsync(id, request, cancellationToken);
        return result.IsFailure
            ? ToErrorResponse(result.Error)
            : Ok(ApiResponse<StartupIndustryResponse>.SuccessResponse(
                result.Value!, "Startup industry updated successfully."));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeStartupIndustryStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.ChangeStatusAsync(id, request, cancellationToken);
        return result.IsFailure
            ? ToErrorResponse(result.Error)
            : Ok(ApiResponse<StartupIndustryResponse>.SuccessResponse(
                result.Value!, "Startup industry status updated successfully."));
    }

    private IActionResult ToErrorResponse(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);
        return error.Code switch
        {
            "STARTUP_INDUSTRY_NOT_FOUND" => NotFound(response),
            "STARTUP_INDUSTRY_NAME_EXISTS" => Conflict(response),
            _ => BadRequest(response)
        };
    }
}
