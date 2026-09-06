using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Workspaces.GetCheckpointOverview;
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

    private static bool IsRole(string role, string expected) =>
        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);
}
