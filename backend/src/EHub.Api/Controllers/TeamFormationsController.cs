using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Teams.TeamFormations;
using EHub.Contracts.Common;
using EHub.Contracts.Teams;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class TeamFormationsController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public TeamFormationsController(ICurrentUserService currentUser) => _currentUser = currentUser;

    [HttpPost("classes/{classId:guid}/team-formations")]
    public async Task<IActionResult> Create(Guid classId, [FromBody] CreateTeamFormationRequest request,
        [FromServices] ITeamFormationHandler handler, CancellationToken cancellationToken) =>
        Respond(await handler.CreateAsync(classId, request, UserId, Role, cancellationToken), "Formation created.", created: true);

    [HttpGet("team-formations/mine")]
    public async Task<IActionResult> Mine([FromQuery] Guid? classId, [FromServices] ITeamFormationHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.GetMineAsync(classId, UserId, Role, cancellationToken), "Formations retrieved.");

    [HttpGet("team-formations/invitations/pending")]
    public async Task<IActionResult> PendingInvitations([FromServices] ITeamFormationHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.GetPendingInvitationsAsync(UserId, Role, cancellationToken), "Pending invitations retrieved.");

    [HttpGet("team-formations/{formationId:guid}")]
    public async Task<IActionResult> Get(Guid formationId, [FromServices] ITeamFormationHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.GetAsync(formationId, UserId, Role, cancellationToken), "Formation retrieved.");

    [HttpPost("team-formations/{formationId:guid}/accept")]
    public async Task<IActionResult> Accept(Guid formationId, [FromServices] ITeamFormationHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.AcceptAsync(formationId, UserId, Role, cancellationToken), "Invitation accepted.");

    [HttpPost("team-formations/{formationId:guid}/decline")]
    public async Task<IActionResult> Decline(Guid formationId, [FromServices] ITeamFormationHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.DeclineAsync(formationId, UserId, Role, cancellationToken), "Invitation declined.");

    [HttpPost("team-formations/{formationId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid formationId, [FromServices] ITeamFormationHandler handler,
        CancellationToken cancellationToken) =>
        Respond(await handler.CancelAsync(formationId, UserId, Role, cancellationToken), "Formation cancelled.");

    private Guid UserId => _currentUser.UserId ?? Guid.Empty;
    private string Role => _currentUser.Roles.FirstOrDefault(role =>
        string.Equals(role, SystemRoles.Student, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

    private IActionResult Respond<T>(Result<T> result, string message, bool created = false)
    {
        if (result.IsSuccess)
        {
            var response = ApiResponse<T>.SuccessResponse(result.Value, message);
            return created ? StatusCode(StatusCodes.Status201Created, response) : Ok(response);
        }
        var failure = ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code);
        if (result.Error.Code == ErrorCodes.ClassAccessDenied) return StatusCode(StatusCodes.Status403Forbidden, failure);
        if (result.Error.Code.EndsWith("_NOT_FOUND", StringComparison.OrdinalIgnoreCase)) return NotFound(failure);
        if (result.Error.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
            result.Error.Code.Contains("STATE_INVALID", StringComparison.OrdinalIgnoreCase) ||
            result.Error.Code.Contains("DUPLICATED", StringComparison.OrdinalIgnoreCase)) return Conflict(failure);
        return BadRequest(failure);
    }
}
