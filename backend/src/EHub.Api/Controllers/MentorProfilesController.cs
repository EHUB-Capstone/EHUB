using EHub.Application.Features.Admin.MentorProfiles;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/admin/mentor-profiles")]
[Authorize(Policy = SystemPolicies.AdminOnly)]
public sealed class MentorProfilesController(IMentorProfileHandler handler) : ControllerBase
{
    [HttpGet("tag-suggestions")]
    public async Task<IActionResult> GetTagSuggestions(CancellationToken cancellationToken) =>
        ToResponse(await handler.GetTagSuggestionsAsync(cancellationToken), "Mentor tag suggestions retrieved successfully.");

    [HttpGet("{mentorProfileId:guid}")]
    public async Task<IActionResult> Get(Guid mentorProfileId, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetAsync(mentorProfileId, cancellationToken), "Mentor profile retrieved successfully.");

    [HttpPut("{mentorProfileId:guid}")]
    public async Task<IActionResult> Update(Guid mentorProfileId, [FromBody] UpdateMentorProfileRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateAsync(mentorProfileId, request, cancellationToken), "Mentor profile updated successfully.");

    private IActionResult ToResponse<T>(Result<T> result, string message) =>
        result.IsSuccess ? Ok(ApiResponse<T>.SuccessResponse(result.Value, message)) : ToError(result.Error);

    private IActionResult ToError(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);
        if (error.Code == ErrorCodes.CommonUnauthorizedError) return Unauthorized(response);
        if (error.Code == ErrorCodes.CommonForbiddenError) return StatusCode(StatusCodes.Status403Forbidden, response);
        if (error.Code.EndsWith("_NOT_FOUND", StringComparison.OrdinalIgnoreCase)) return NotFound(response);
        if (error.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase)) return Conflict(response);
        return BadRequest(response);
    }
}
