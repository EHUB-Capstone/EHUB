using EHub.Application.Features.Admin.Users.ManageUsers;
using EHub.Contracts.Common;
using EHub.Contracts.Users;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = SystemPolicies.AdminOnly)]
public sealed class UsersController(IUserManagementHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int limit = 10, [FromQuery] string? search = null, [FromQuery] string? role = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var result = await handler.GetUsersAsync(page, limit, search, role, status, cancellationToken);
        return ToActionResult(result, "Users retrieved successfully.");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await handler.GetUserAsync(id, cancellationToken);
        return ToActionResult(result, "User retrieved successfully.");
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] SaveManagedUserRequest request, CancellationToken cancellationToken)
    {
        var result = await handler.CreateUserAsync(request, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetUser), new { id = result.Value!.Id }, ApiResponse<ManagedUserResponse>.SuccessResponse(result.Value, "User created successfully.")) : ToError(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] SaveManagedUserRequest request, CancellationToken cancellationToken)
    {
        var result = await handler.UpdateUserAsync(id, request, cancellationToken);
        return ToActionResult(result, "User updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var result = await handler.DeleteUserAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(ApiResponse<object?>.SuccessResponse(null, "User deleted successfully.")) : ToError(result.Error);
    }

    private IActionResult ToActionResult<T>(EHub.Shared.Results.Result<T> result, string message) => result.IsSuccess ? Ok(ApiResponse<T>.SuccessResponse(result.Value!, message)) : ToError(result.Error);
    private IActionResult ToError(EHub.Shared.Errors.Error error) => error.Code == "NOT_FOUND" ? NotFound(ApiResponse<object>.FailureResponse(error.Message, error.Code)) : error.Code is "RELATED_DATA_EXISTS" ? Conflict(ApiResponse<object>.FailureResponse(error.Message, error.Code)) : BadRequest(ApiResponse<object>.FailureResponse(error.Message, error.Code));
}
