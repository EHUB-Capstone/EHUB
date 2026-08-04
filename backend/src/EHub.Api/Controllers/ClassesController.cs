using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Classes.CreateBulkClasses;
using EHub.Application.Features.Classes.CreateClass;
using EHub.Application.Features.Classes.GetClasses;
using EHub.Application.Features.Classes.UpdateClass;
using EHub.Application.Features.Classes.UpdateClassSchedule;
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
        var currentUserRole = GetCurrentUserRole();

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

    [HttpPost]
    public async Task<IActionResult> CreateClass(
        [FromBody] CreateClassRequest request,
        [FromServices] ICreateClassCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await commandHandler.HandleAsync(
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

        return CreatedAtAction(
            nameof(GetClasses),
            new { id = result.Value.Id },
            ApiResponse<ClassResponse>.SuccessResponse(result.Value, "Class created successfully."));
    }

    [HttpPost("bulk/preview")]
    public async Task<IActionResult> PreviewBulkClasses(
        [FromBody] CreateBulkClassesRequest request,
        [FromServices] ICreateBulkClassesCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await commandHandler.PreviewAsync(
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

        return Ok(ApiResponse<BulkClassPreviewResponse>.SuccessResponse(
            result.Value,
            "Bulk classes preview generated successfully."));
    }

    [HttpPost("bulk/commit")]
    public async Task<IActionResult> CreateBulkClasses(
        [FromBody] CreateBulkClassesRequest request,
        [FromServices] ICreateBulkClassesCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await commandHandler.CommitAsync(
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

        return Ok(ApiResponse<IReadOnlyCollection<ClassResponse>>.SuccessResponse(
            result.Value,
            $"{result.Value.Count} classes created successfully in batch."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateClass(
        Guid id,
        [FromBody] UpdateClassRequest request,
        [FromServices] IUpdateClassCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await commandHandler.HandleAsync(
            id,
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

        return Ok(ApiResponse<ClassResponse>.SuccessResponse(
            result.Value,
            "Class information updated successfully."));
    }

    [HttpPut("{id:guid}/schedule")]
    public async Task<IActionResult> UpdateClassSchedule(
        Guid id,
        [FromBody] UpdateClassScheduleRequest request,
        [FromServices] IUpdateClassScheduleCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await commandHandler.HandleAsync(
            id,
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

            if (result.Error.Code.Contains("ScheduleConflict", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
            }

            return BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ClassResponse>.SuccessResponse(
            result.Value,
            "Class schedule updated successfully."));
    }

    [HttpPut("{id:guid}/teaching-assignment")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateTeachingAssignment(
        Guid id,
        [FromBody] UpdateTeachingAssignmentRequest request,
        [FromServices] IUpdateClassCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await commandHandler.UpdateTeachingAssignmentAsync(
            id,
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

        return Ok(ApiResponse<ClassResponse>.SuccessResponse(
            result.Value,
            "Teaching assignment updated successfully."));
    }

    private string GetCurrentUserRole()
    {
        return _currentUserService.Roles.FirstOrDefault()
            ?? User.FindFirstValue(ClaimNames.Role)
            ?? User.FindFirstValue(ClaimTypes.Role)
            ?? string.Empty;
    }
}
