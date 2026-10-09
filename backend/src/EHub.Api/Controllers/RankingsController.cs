using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Rankings.TeamRankings;
using EHub.Contracts.Common;
using EHub.Contracts.Rankings;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/rankings")]
[Authorize(Policy = SystemPolicies.StaffOnly)]
public sealed class RankingsController(
    ICurrentUserService currentUser,
    ITeamRankingQueryHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRankings(
        [FromQuery] GetTeamRankingsRequest request,
        CancellationToken cancellationToken)
    {
        var role = currentUser.Roles.Any(item =>
            string.Equals(item, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
            ? SystemRoles.Admin
            : SystemRoles.Lecturer;
        var result = await handler.HandleAsync(
            request,
            currentUser.UserId ?? Guid.Empty,
            role,
            cancellationToken);

        return result.IsSuccess
            ? Ok(ApiResponse<TeamRankingListResponse>.SuccessResponse(
                result.Value, "Team rankings retrieved."))
            : StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
    }
}
