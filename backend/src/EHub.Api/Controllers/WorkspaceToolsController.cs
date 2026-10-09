using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Workspaces;
using EHub.Contracts.Common;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class WorkspaceToolsController(ICurrentUserService currentUser, IWorkspaceToolsHandler handler) : ControllerBase
{
    [HttpGet("weekly-tasks")]
    public async Task<IActionResult> GetWeeklyTasks([FromQuery] WeeklyTaskQuery query, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetWeeklyTasksAsync(query, UserId, Role, cancellationToken), "Weekly roadmap retrieved.");

    [HttpGet("weekly-tasks/team/{teamId:guid}/board")]
    public async Task<IActionResult> GetTeamBoard(Guid teamId, [FromQuery] WeeklyTaskQuery query, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetWeeklyTasksAsync(new WeeklyTaskQuery { CourseCode = query.CourseCode, WeekNumber = query.WeekNumber, ClassId = query.ClassId, TeamId = teamId, Status = query.Status, AssigneeStudentId = query.AssigneeStudentId, Priority = query.Priority, Search = query.Search }, UserId, Role, cancellationToken), "Team task board retrieved.");

    [HttpPost("weekly-tasks")]
    public async Task<IActionResult> CreateWeeklyTask([FromBody] SaveWeeklyTaskRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.CreateWeeklyTaskAsync(request, UserId, Role, cancellationToken), "Weekly task created.", true);

    [HttpPut("weekly-tasks/{taskId:guid}")]
    public async Task<IActionResult> UpdateWeeklyTask(Guid taskId, [FromBody] SaveWeeklyTaskRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateWeeklyTaskAsync(taskId, request, UserId, Role, cancellationToken), "Weekly task updated.");

    [HttpPatch("weekly-tasks/{taskId:guid}/status")]
    public async Task<IActionResult> UpdateWeeklyTaskStatus(Guid taskId, [FromBody] UpdateWeeklyTaskStatusRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateWeeklyTaskStatusAsync(taskId, request, UserId, Role, cancellationToken), "Weekly task status updated.");

    [HttpDelete("weekly-tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteWeeklyTask(Guid taskId, CancellationToken cancellationToken) =>
        ToResponse(await handler.DeleteWeeklyTaskAsync(taskId, UserId, Role, cancellationToken), "Weekly task deleted.");

    [HttpGet("teams/{teamId:guid}/shortcuts")]
    public async Task<IActionResult> GetShortcuts(Guid teamId, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetShortcutsAsync(teamId, UserId, Role, cancellationToken), "Shortcuts retrieved.");

    [HttpPost("teams/{teamId:guid}/shortcuts")]
    public async Task<IActionResult> CreateShortcut(Guid teamId, [FromBody] SaveShortcutRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.CreateShortcutAsync(teamId, request, UserId, Role, cancellationToken), "Shortcut created.", true);

    [HttpPut("teams/{teamId:guid}/shortcuts/{shortcutId:guid}")]
    public async Task<IActionResult> UpdateShortcut(Guid teamId, Guid shortcutId, [FromBody] SaveShortcutRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateShortcutAsync(teamId, shortcutId, request, UserId, Role, cancellationToken), "Shortcut updated.");

    [HttpDelete("teams/{teamId:guid}/shortcuts/{shortcutId:guid}")]
    public async Task<IActionResult> DeleteShortcut(Guid teamId, Guid shortcutId, CancellationToken cancellationToken) =>
        ToResponse(await handler.DeleteShortcutAsync(teamId, shortcutId, UserId, Role, cancellationToken), "Shortcut deleted.");

    private Guid UserId => currentUser.UserId ?? Guid.Empty;
    private string Role => SystemRoles.All.FirstOrDefault(expected => currentUser.Roles.Any(role => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase))) ?? string.Empty;

    private IActionResult ToResponse<T>(Result<T> result, string message, bool created = false)
    {
        if (result.IsSuccess)
        {
            var response = ApiResponse<T>.SuccessResponse(result.Value, message);
            return created ? StatusCode(StatusCodes.Status201Created, response) : Ok(response);
        }
        return Failure(result.Error);
    }

    private IActionResult ToResponse(Result result, string message)
    {
        if (result.IsSuccess) return Ok(ApiResponse<object?>.SuccessResponse(null, message));
        return Failure(result.Error);
    }

    private IActionResult Failure(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);
        return error.Code switch
        {
            ErrorCodes.WorkspaceAccessDenied => StatusCode(StatusCodes.Status403Forbidden, response),
            ErrorCodes.WorkspaceNotFound or ErrorCodes.TeamNotFound or ErrorCodes.WeeklyTaskNotFound or ErrorCodes.ShortcutNotFound => NotFound(response),
            ErrorCodes.WorkspaceTagDuplicated or ErrorCodes.WeeklyTaskDuplicated or ErrorCodes.ShortcutDuplicated => Conflict(response),
            _ => BadRequest(response)
        };
    }
}
