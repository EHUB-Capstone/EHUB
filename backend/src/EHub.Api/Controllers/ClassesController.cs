using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Classes.GetClasses;
using EHub.Contracts.Classes;
using EHub.Contracts.Common;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize(Policy = SystemPolicies.StaffOnly)]
public sealed class ClassesController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;

    public ClassesController(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetClasses(
        [FromQuery] GetClassesRequest request,
        [FromServices] IGetClassesQueryHandler queryHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        var result = await queryHandler.HandleAsync(
            request,
            currentUserId,
            currentUserRole,
            cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
            }

            return BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ClassListResponse>.SuccessResponse(
            result.Value,
            "Classes retrieved successfully."));
    }
}
