using EHub.Application.Features.Admin.Users.ManageUsers;
using EHub.Application.Features.Admin.Users.ImportLecturers;
using EHub.Contracts.Common;
using EHub.Contracts.Users;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = SystemPolicies.StaffOnly)]
public sealed class UsersController(IUserManagementHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.GetUsersAsync(
            page,
            limit,
            search,
            role,
            status,
            cancellationToken);

        return ToActionResult(result, "Users retrieved successfully.");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetUserAsync(id, cancellationToken);

        return ToActionResult(result, "User retrieved successfully.");
    }

    [HttpPost]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> CreateUser(
        [FromBody] SaveManagedUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.CreateUserAsync(request, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(
                nameof(GetUser),
                new { id = result.Value!.Id },
                ApiResponse<ManagedUserResponse>.SuccessResponse(
                    result.Value,
                    "User created successfully."))
            : ToError(result.Error);
    }

    [HttpPost("import-lecturers/preview")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PreviewLecturerImport(
        [FromForm] IFormFile file,
        [FromServices] ILecturerImportHandler importHandler,
        CancellationToken cancellationToken)
    {
        var result = await importHandler.PreviewAsync(file, cancellationToken);

        return ToActionResult(result, "Lecturer import preview generated successfully.");
    }

    [HttpPost("import-lecturers/commit")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> CommitLecturerImport(
        [FromBody] CommitLecturerImportRequest request,
        [FromServices] ILecturerImportHandler importHandler,
        CancellationToken cancellationToken)
    {
        var result = await importHandler.CommitAsync(request, cancellationToken);

        return ToActionResult(result, "Lecturer accounts imported successfully.");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateUser(
        Guid id,
        [FromBody] SaveManagedUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await handler.UpdateUserAsync(
            id,
            request,
            cancellationToken);

        return ToActionResult(result, "User updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> DeleteUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await handler.DeleteUserAsync(id, cancellationToken);

        return result.IsSuccess
            ? Ok(ApiResponse<object?>.SuccessResponse(
                null,
                "User deleted successfully."))
            : ToError(result.Error);
    }

    private IActionResult ToActionResult<T>(
        EHub.Shared.Results.Result<T> result,
        string message)
    {
        return result.IsSuccess
            ? Ok(ApiResponse<T>.SuccessResponse(result.Value!, message))
            : ToError(result.Error);
    }

    private IActionResult ToError(EHub.Shared.Errors.Error error)
    {
        return error.Code switch
        {
            "NOT_FOUND" => NotFound(
                ApiResponse<object>.FailureResponse(
                    error.Message,
                    error.Code)),

            "RELATED_DATA_EXISTS" => Conflict(
                ApiResponse<object>.FailureResponse(
                    error.Message,
                    error.Code)),

            EHub.Shared.Errors.ErrorCodes.LecturerImportSessionExpired or
            EHub.Shared.Errors.ErrorCodes.LecturerImportSessionInvalid or
            EHub.Shared.Errors.ErrorCodes.LecturerImportSessionAlreadyProcessing or
            EHub.Shared.Errors.ErrorCodes.LecturerImportConflict => Conflict(
                ApiResponse<object>.FailureResponse(
                    error.Message,
                    error.Code)),

            EHub.Shared.Errors.ErrorCodes.CommonUnauthorizedError => Unauthorized(
                ApiResponse<object>.FailureResponse(
                    error.Message,
                    error.Code)),

            _ => BadRequest(
                ApiResponse<object>.FailureResponse(
                    error.Message,
                    error.Code))
        };
    }
}
