using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Classes.AddStudentToClass;
using EHub.Application.Features.Classes.CreateBulkClasses;
using EHub.Application.Features.Classes.CreateClass;
using EHub.Application.Features.Classes.ExportClassRoster;
using EHub.Application.Features.Classes.GetClassDetail;
using EHub.Application.Features.Classes.GetClasses;
using EHub.Application.Features.Classes.GetClassRoster;
using EHub.Application.Features.Classes.GetImportTemplate;
using EHub.Application.Features.Classes.ImportStudents;
using EHub.Application.Features.Classes.RemoveStudentFromClass;
using EHub.Application.Features.Classes.UpdateClass;
using EHub.Application.Features.Classes.UpdateClassSchedule;
using EHub.Application.Features.Classes.UpdateClassStudent;
using EHub.Contracts.Classes;
using EHub.Contracts.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

    [HttpGet("import-template")]
    public async Task<IActionResult> GetImportTemplate(
        [FromServices] IGetImportTemplateQueryHandler queryHandler,
        CancellationToken cancellationToken)
    {
        var result = await queryHandler.HandleAsync(cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
        }

        return File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetClassDetail(
        Guid id,
        [FromServices] IGetClassDetailQueryHandler queryHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await queryHandler.HandleAsync(
            id,
            currentUserId,
            currentUserRole,
            cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
            }

            if (result.Error.Code.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
            }

            return BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ClassResponse>.SuccessResponse(
            result.Value,
            "Class details retrieved successfully."));
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
            return ToClassErrorResponse(result.Error);
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
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<ClassResponse>.SuccessResponse(
            result.Value,
            "Teaching assignment updated successfully."));
    }

    // ─── ROSTER MANAGEMENT ENDPOINTS ──────────────────────────────────────────

    [HttpGet("{id:guid}/students")]
    public async Task<IActionResult> GetClassRoster(
        Guid id,
        [FromQuery] GetClassRosterRequest request,
        [FromServices] IGetClassRosterQueryHandler queryHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await queryHandler.HandleAsync(
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

        return Ok(ApiResponse<ClassRosterListResponse>.SuccessResponse(
            result.Value,
            "Class roster retrieved successfully."));
    }

    [HttpPost("{id:guid}/students")]
    public async Task<IActionResult> AddStudentToClass(
        Guid id,
        [FromBody] AddStudentToClassRequest request,
        [FromServices] IAddStudentToClassCommandHandler commandHandler,
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
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<ClassStudentDto>.SuccessResponse(
            result.Value,
            "Student added to class successfully."));
    }

    [HttpPut("{id:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> UpdateClassStudent(
        Guid id,
        Guid studentId,
        [FromBody] UpdateClassStudentRequest request,
        [FromServices] IUpdateClassStudentCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await commandHandler.HandleAsync(
            id,
            studentId,
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

        return Ok(ApiResponse<ClassStudentDto>.SuccessResponse(
            result.Value,
            "Class student updated successfully."));
    }

    [HttpDelete("{id:guid}/students/{studentId:guid}")]
    public async Task<IActionResult> RemoveStudentFromClass(
        Guid id,
        Guid studentId,
        [FromServices] IRemoveStudentFromClassCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await commandHandler.HandleAsync(
            id,
            studentId,
            currentUserId,
            currentUserRole,
            cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
            }

            if (result.Error.Code.Contains("StudentInActiveTeam", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Code.Contains("StudentIsTeamLeader", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
            }

            return BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<object?>.SuccessResponse(null, "Student removed from class successfully."));
    }

    // ─── GIAI ĐOẠN 5: EXCEL IMPORT & EXPORT ENDPOINTS ─────────────────────────

    [HttpPost("{id:guid}/import-students/preview")]
    public async Task<IActionResult> PreviewImportStudents(
        Guid id,
        IFormFile file,
        [FromServices] IPreviewImportStudentsCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await commandHandler.HandleAsync(
            id,
            file,
            currentUserId,
            currentUserRole,
            cancellationToken);

        if (result.IsFailure)
        {
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<ImportStudentsPreviewResponse>.SuccessResponse(
            result.Value,
            "Import preview generated successfully."));
    }

    [HttpPost("{id:guid}/import-students/commit")]
    public async Task<IActionResult> CommitImportStudents(
        Guid id,
        [FromBody] CommitImportStudentsRequest request,
        [FromServices] ICommitImportStudentsCommandHandler commandHandler,
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
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<ImportStudentsCommitResponse>.SuccessResponse(
            result.Value,
            "Students imported successfully."));
    }

    [HttpGet("{id:guid}/export-students")]
    [HttpGet("{id:guid}/export-excel")]
    public async Task<IActionResult> ExportClassRoster(
        Guid id,
        [FromServices] IExportClassRosterQueryHandler queryHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await queryHandler.HandleAsync(
            id,
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

        return File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName);
    }

    private IActionResult ToClassErrorResponse(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);

        return error.Code switch
        {
            ErrorCodes.ClassAccessDenied => StatusCode(StatusCodes.Status403Forbidden, response),
            ErrorCodes.ClassNotFound => NotFound(response),
            ErrorCodes.ClassScheduleConflict or
            ErrorCodes.ClassConcurrencyConflict or
            ErrorCodes.ClassLecturerRequired or
            ErrorCodes.ClassArchived or
            ErrorCodes.ClassStudentIdentityConflict or
            ErrorCodes.ClassStudentAlreadyEnrolled or
            ErrorCodes.ClassStudentEnrollmentConflict or
            ErrorCodes.ClassEnrollmentMajorLocked or
            ErrorCodes.ClassImportSessionInvalid or
            ErrorCodes.ClassImportSessionExpired or
            ErrorCodes.ClassImportSessionAlreadyProcessing => Conflict(response),
            _ => BadRequest(response)
        };
    }

    private string GetCurrentUserRole()
    {
        if (_currentUserService.Roles.Any(role => string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase)))
        {
            return SystemRoles.Admin;
        }

        if (_currentUserService.Roles.Any(role => string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase)))
        {
            return SystemRoles.Lecturer;
        }

        return User.FindFirstValue(ClaimNames.Role)
            ?? User.FindFirstValue(ClaimTypes.Role)
            ?? _currentUserService.Roles.FirstOrDefault()
            ?? string.Empty;
    }
}
