using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.ProjectData.GetProjectAchievementHistory;
using EHub.Application.Features.ProjectData.GetProjectData;
using EHub.Application.Features.ProjectData.GetProjectDataFilterOptions;
using EHub.Application.Features.ProjectData.GetProjectDataSummary;
using EHub.Application.Features.ProjectData.ManageAchievements;
using EHub.Contracts.Common;
using EHub.Contracts.ProjectData;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/project-data")]
[Authorize(Policy = SystemPolicies.StaffOnly)]
public sealed class ProjectDataController(ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetProjectData(
        [FromQuery] GetProjectDataRequest request,
        [FromServices] IGetProjectDataQueryHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(
            await handler.HandleAsync(request, UserId, Role, cancellationToken),
            "Project data retrieved.");

    [HttpGet("summary")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] GetProjectDataRequest request,
        [FromServices] IGetProjectDataSummaryQueryHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(
            await handler.HandleAsync(request, UserId, Role, cancellationToken),
            "Project data summary retrieved.");

    [HttpGet("filter-options")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetFilterOptions(
        [FromServices] IGetProjectDataFilterOptionsQueryHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(
            await handler.HandleAsync(UserId, Role, cancellationToken),
            "Project data filter options retrieved.");

    [HttpGet("{projectId:guid}/achievements/history")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetAchievementHistory(
        Guid projectId,
        [FromServices] IGetProjectAchievementHistoryQueryHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(
            await handler.HandleAsync(projectId, UserId, Role, cancellationToken),
            "Project achievement history retrieved.");

    [HttpPut("{projectId:guid}/achievements")]
    public async Task<IActionResult> UpdateAchievements(
        Guid projectId,
        [FromBody] UpdateProjectAchievementsRequest request,
        [FromServices] IUpdateProjectAchievementsCommandHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(
            await handler.HandleAsync(projectId, request, UserId, Role, cancellationToken),
            "Project achievements updated.");

    private Guid UserId => currentUser.UserId ?? Guid.Empty;

    private string Role => currentUser.Roles.Any(role =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        ? SystemRoles.Admin
        : currentUser.Roles.Any(role => string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase))
            ? SystemRoles.Lecturer
            : string.Empty;

    private IActionResult ToResponse<T>(Result<T> result, string message)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse<T>.SuccessResponse(result.Value, message));

        var failure = ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code);
        return result.Error.Code switch
        {
            ErrorCodes.ProjectDataAccessDenied => StatusCode(StatusCodes.Status403Forbidden, failure),
            ErrorCodes.ProjectDataNotFound => NotFound(failure),
            ErrorCodes.ProjectDataConcurrencyConflict => Conflict(failure),
            _ => BadRequest(failure),
        };
    }
}
