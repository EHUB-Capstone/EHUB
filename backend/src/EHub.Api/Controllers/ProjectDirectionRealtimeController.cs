using EHub.Application.Common.Interfaces.Identity;
using EHub.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/realtime/project-directions")]
[Authorize]
public sealed class ProjectDirectionRealtimeController(
    ICurrentUserService currentUser,
    ProjectDirectionRealtimeService realtimeService) : ControllerBase
{
    [HttpGet]
    public async Task Get(CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest || !currentUser.UserId.HasValue)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var socket = await HttpContext.WebSockets.AcceptWebSocketAsync("ehub-project-directions");
        await realtimeService.ListenAsync(currentUser.UserId.Value, socket, cancellationToken);
    }
}
