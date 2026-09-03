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
public sealed class ProjectWorkspacesController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public ProjectWorkspacesController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet("workspace/accessible-teams")]
    public async Task<IActionResult> GetAccessibleTeams(
        [FromServices] IProjectWorkspaceHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.GetAccessibleAsync(UserId, Role, cancellationToken), "Accessible workspaces retrieved.");

    [HttpGet("team-workspaces/current")]
    public async Task<IActionResult> GetCurrentWorkspace(
        [FromServices] IProjectWorkspaceHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.GetCurrentContextAsync(UserId, Role, cancellationToken), "Current workspace context retrieved.");

    [HttpGet("team-workspaces/team/{teamId:guid}/context")]
    public async Task<IActionResult> GetWorkspaceContext(
        Guid teamId,
        [FromServices] IProjectWorkspaceHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.GetContextAsync(teamId, UserId, Role, cancellationToken), "Workspace context retrieved.");

    [HttpGet("workspace/teams/{teamId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetWorkspace(
        Guid teamId,
        [FromServices] IProjectWorkspaceHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.GetDetailAsync(teamId, UserId, Role, cancellationToken), "Workspace retrieved.");

    [HttpPost("workspace/teams/{teamId:guid}")]
    public async Task<IActionResult> CreateWorkspace(
        Guid teamId,
        [FromBody] CreateProjectWorkspaceRequest request,
        [FromServices] IProjectWorkspaceHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.CreateAsync(teamId, request, UserId, Role, cancellationToken), "Project workspace created.", created: true);

    [HttpPut("workspace/teams/{teamId:guid}/profile")]
    public async Task<IActionResult> UpdateWorkspaceProfile(
        Guid teamId,
        [FromBody] UpdateProjectWorkspaceRequest request,
        [FromServices] IProjectWorkspaceHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateAsync(teamId, request, UserId, Role, cancellationToken), "Project profile updated.");

    private Guid UserId => _currentUser.UserId ?? Guid.Empty;

    private string Role
    {
        get
        {
            if (_currentUser.Roles.Any(role => string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))) return SystemRoles.Admin;
            if (_currentUser.Roles.Any(role => string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase))) return SystemRoles.Lecturer;
            if (_currentUser.Roles.Any(role => string.Equals(role, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase))) return SystemRoles.Mentor;
            if (_currentUser.Roles.Any(role => string.Equals(role, SystemRoles.Student, StringComparison.OrdinalIgnoreCase))) return SystemRoles.Student;
            return string.Empty;
        }
    }

    private IActionResult ToResponse<T>(Result<T> result, string message, bool created = false)
    {
        if (result.IsSuccess)
        {
            var response = ApiResponse<T>.SuccessResponse(result.Value, message);
            return created ? StatusCode(StatusCodes.Status201Created, response) : Ok(response);
        }

        var failure = ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code);
        return result.Error.Code switch
        {
            ErrorCodes.WorkspaceAccessDenied or ErrorCodes.WorkspaceLeaderRequired => StatusCode(StatusCodes.Status403Forbidden, failure),
            ErrorCodes.TeamNotFound or ErrorCodes.WorkspaceNotFound => NotFound(failure),
            ErrorCodes.WorkspaceAlreadyExists or ErrorCodes.WorkspaceConcurrencyConflict or ErrorCodes.ClassArchived or ErrorCodes.ClassCompleted => Conflict(failure),
            _ => BadRequest(failure)
        };
    }
}
