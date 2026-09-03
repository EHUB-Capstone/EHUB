using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Classes.AddStudentToClass;
using EHub.Application.Features.Classes.AssignStudents;
using EHub.Application.Features.Classes.CreateBulkClasses;
using EHub.Application.Features.Classes.CreateClass;
using EHub.Application.Features.Classes.ClassAudit;
using EHub.Application.Features.Classes.ClassLifecycle;
using EHub.Application.Features.Classes.ClassCompletion;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.ExportClassRoster;
using EHub.Application.Features.Classes.GetClassDetail;
using EHub.Application.Features.Classes.GetClasses;
using EHub.Application.Features.Classes.GetClassRoster;
using EHub.Application.Features.Classes.GetImportTemplate;
using EHub.Application.Features.Classes.GetMajorVerificationTemplate;
using EHub.Application.Features.Classes.ImportStudents;
using EHub.Application.Features.Classes.RemoveStudentFromClass;
using EHub.Application.Features.Classes.ReEnrollStudent;
using EHub.Application.Features.Classes.RepairChatMemberships;
using EHub.Application.Features.Classes.SetEnrollmentMajorLock;
using EHub.Application.Features.Classes.SynchronizeProfileMajors;
using EHub.Application.Features.Classes.UpdateClass;
using EHub.Application.Features.Classes.UpdateClassSchedule;
using EHub.Application.Features.Classes.UpdateClassStudent;
using EHub.Application.Features.Classes.VerifyClassMajors;
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
            return ToClassErrorResponse(result.Error);
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
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<ClassResponse>.SuccessResponse(
            result.Value,
            "Class details retrieved successfully."));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetClassDetailBySlug(
        string slug,
        [FromServices] IGetClassDetailQueryHandler queryHandler,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var result = await queryHandler.HandleAsync(
            slug,
            currentUserId,
            currentUserRole,
            cancellationToken);

        if (result.IsFailure)
        {
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<ClassResponse>.SuccessResponse(
            result.Value,
            "Class details retrieved successfully."));
    }
    [HttpPost]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
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
            return ToClassErrorResponse(result.Error);
        }

        return CreatedAtAction(
            nameof(GetClassDetail),
            new { id = result.Value.Id },
            ApiResponse<ClassResponse>.SuccessResponse(result.Value, "Class created successfully."));
    }

    [HttpPost("bulk/preview")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
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
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<BulkClassPreviewResponse>.SuccessResponse(
            result.Value,
            "Bulk classes preview generated successfully."));
    }

    [HttpPost("bulk/commit")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
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
            return ToClassErrorResponse(result.Error);
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
            return ToClassErrorResponse(result.Error);
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
            return ToClassErrorResponse(result.Error);
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

    [HttpPost("{id:guid}/students/assign")]
    public async Task<IActionResult> AssignStudentsToClass(
        Guid id,
        [FromBody] AssignStudentsToClassRequest request,
        [FromServices] IAssignStudentsCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.AssignToClassAsync(
            id,
            request,
            _currentUserService.UserId ?? Guid.Empty,
            GetCurrentUserRole(),
            cancellationToken);
        if (result.IsFailure)
        {
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<ClassStudentAssignmentResponse>.SuccessResponse(
            result.Value,
            "Students assigned to class successfully."));
    }

    [HttpPost("{id:guid}/teams/{teamId:guid}/students/assign")]
    public async Task<IActionResult> AssignStudentsToTeam(
        Guid id,
        Guid teamId,
        [FromBody] AssignStudentsToTeamRequest request,
        [FromServices] IAssignStudentsCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.AssignToTeamAsync(
            id,
            teamId,
            request,
            _currentUserService.UserId ?? Guid.Empty,
            GetCurrentUserRole(),
            cancellationToken);
        if (result.IsFailure)
        {
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<TeamStudentAssignmentResponse>.SuccessResponse(
            result.Value,
            "Students assigned to team successfully."));
    }

    [HttpPut("{id:guid}/students/{studentId:guid}/major")]
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
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<ClassStudentDto>.SuccessResponse(
            result.Value,
            "Class student updated successfully."));
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveClass(
        Guid id,
        [FromBody] ChangeClassLifecycleRequest request,
        [FromServices] IClassLifecycleCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.ArchiveAsync(
            id, request, _currentUserService.UserId ?? Guid.Empty, GetCurrentUserRole(), cancellationToken);
        if (result.IsFailure) return ToClassErrorResponse(result.Error);
        return Ok(ApiResponse<ClassLifecycleResponse>.SuccessResponse(result.Value, "Class archived successfully."));
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreClass(
        Guid id,
        [FromBody] ChangeClassLifecycleRequest request,
        [FromServices] IClassLifecycleCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.RestoreAsync(
            id, request, _currentUserService.UserId ?? Guid.Empty, GetCurrentUserRole(), cancellationToken);
        if (result.IsFailure) return ToClassErrorResponse(result.Error);
        return Ok(ApiResponse<ClassLifecycleResponse>.SuccessResponse(result.Value, "Class restored successfully."));
    }

    [HttpGet("{id:guid}/completion-preview")]
    public async Task<IActionResult> PreviewClassCompletion(
        Guid id,
        [FromServices] IClassCompletionCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.PreviewAsync(
            id, _currentUserService.UserId ?? Guid.Empty, GetCurrentUserRole(), cancellationToken);
        if (result.IsFailure) return ToClassErrorResponse(result.Error);

        return Ok(ApiResponse<ClassCompletionPreviewResponse>.SuccessResponse(
            result.Value,
            "Class completion preview generated successfully."));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteClass(
        Guid id,
        [FromBody] ChangeClassLifecycleRequest request,
        [FromServices] IClassCompletionCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.CompleteAsync(
            id, request, _currentUserService.UserId ?? Guid.Empty, GetCurrentUserRole(), cancellationToken);
        if (result.IsFailure) return ToClassErrorResponse(result.Error);

        return Ok(ApiResponse<ClassLifecycleResponse>.SuccessResponse(
            result.Value,
            "Class completed successfully."));
    }

    [HttpPost("{id:guid}/reopen")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> ReopenClass(
        Guid id,
        [FromBody] ChangeClassLifecycleRequest request,
        [FromServices] IClassCompletionCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.ReopenAsync(
            id, request, _currentUserService.UserId ?? Guid.Empty, GetCurrentUserRole(), cancellationToken);
        if (result.IsFailure) return ToClassErrorResponse(result.Error);

        return Ok(ApiResponse<ClassLifecycleResponse>.SuccessResponse(
            result.Value,
            "Class reopened successfully."));
    }

    [HttpPost("{id:guid}/repair-chat-memberships")]
    public async Task<IActionResult> RepairChatMemberships(
        Guid id,
        [FromServices] IRepairClassChatMembershipsCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.HandleAsync(
            id,
            _currentUserService.UserId ?? Guid.Empty,
            GetCurrentUserRole(),
            cancellationToken);
        if (result.IsFailure) return ToClassErrorResponse(result.Error);

        return Ok(ApiResponse<ChatMembershipSyncResponse>.SuccessResponse(
            result.Value,
            "Class chat memberships repaired successfully."));
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> GetClassAudit(
        Guid id,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IGetClassAuditQueryHandler queryHandler,
        CancellationToken cancellationToken)
    {
        var result = await queryHandler.HandleAsync(
            id, page == 0 ? 1 : page, pageSize == 0 ? 25 : pageSize,
            _currentUserService.UserId ?? Guid.Empty, GetCurrentUserRole(), cancellationToken);
        if (result.IsFailure) return ToClassErrorResponse(result.Error);
        return Ok(ApiResponse<ClassAuditLogListResponse>.SuccessResponse(result.Value, "Class audit trail retrieved successfully."));
    }

    [HttpPost("{id:guid}/major-verification")]
    public async Task<IActionResult> VerifyClassMajors(
        Guid id,
        IFormFile file,
        [FromServices] IVerifyClassMajorsCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.HandleAsync(
            id,
            file,
            _currentUserService.UserId ?? Guid.Empty,
            GetCurrentUserRole(),
            cancellationToken);

        if (result.IsFailure)
        {
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<VerifyClassMajorsResponse>.SuccessResponse(
            result.Value,
            "Enrollment majors verified successfully."));
    }

    [HttpGet("major-verification-template")]
    public IActionResult GetMajorVerificationTemplate(
        [FromServices] IGetMajorVerificationTemplateQueryHandler queryHandler)
    {
        var result = queryHandler.Handle();
        return File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("{id:guid}/students/synchronize-profile-majors")]
    public async Task<IActionResult> SynchronizeProfileMajors(
        Guid id,
        [FromServices] ISynchronizeProfileMajorsCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.HandleAsync(
            id,
            _currentUserService.UserId ?? Guid.Empty,
            GetCurrentUserRole(),
            cancellationToken);
        if (result.IsFailure) return ToClassErrorResponse(result.Error);

        return Ok(ApiResponse<SynchronizeProfileMajorsResponse>.SuccessResponse(
            result.Value,
            $"Synchronized {result.Value.SynchronizedCount} registered major(s)."));
    }

    [HttpPost("{id:guid}/major-lock")]
    public Task<IActionResult> LockEnrollmentMajors(
        Guid id,
        [FromServices] ISetEnrollmentMajorLockCommandHandler commandHandler,
        CancellationToken cancellationToken) =>
        SetEnrollmentMajorLock(id, true, commandHandler, cancellationToken);

    [HttpDelete("{id:guid}/major-lock")]
    public Task<IActionResult> UnlockEnrollmentMajors(
        Guid id,
        [FromServices] ISetEnrollmentMajorLockCommandHandler commandHandler,
        CancellationToken cancellationToken) =>
        SetEnrollmentMajorLock(id, false, commandHandler, cancellationToken);

    [HttpPost("{id:guid}/students/{studentId:guid}/drop")]
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
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<object?>.SuccessResponse(null, "Student enrollment dropped successfully."));
    }

    [HttpPost("{id:guid}/students/{studentId:guid}/re-enroll")]
    public async Task<IActionResult> ReEnrollStudent(
        Guid id,
        Guid studentId,
        [FromServices] IReEnrollStudentCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.HandleAsync(
            id,
            studentId,
            _currentUserService.UserId ?? Guid.Empty,
            GetCurrentUserRole(),
            cancellationToken);

        if (result.IsFailure)
        {
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<ClassStudentDto>.SuccessResponse(result.Value, "Student re-enrolled successfully."));
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
        [FromQuery] ExportClassRosterRequest request,
        [FromServices] IExportClassRosterQueryHandler queryHandler,
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
            return ToClassErrorResponse(result.Error);
        }

        return File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName);
    }

    private IActionResult ToClassErrorResponse(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);

        return error.Code switch
        {
            ErrorCodes.ClassAccessDenied => StatusCode(StatusCodes.Status403Forbidden, response),
            ErrorCodes.ClassChatMembershipRepairFailed => StatusCode(StatusCodes.Status500InternalServerError, response),
            ErrorCodes.ClassNotFound or
            ErrorCodes.ClassStudentNotFound or
            ErrorCodes.ClassAssignmentStudentNotFound or
            ErrorCodes.TeamNotFound => NotFound(response),
            ErrorCodes.ClassScheduleConflict or
            ErrorCodes.ClassConcurrencyConflict or
            ErrorCodes.ClassCodeDuplicated or
            ErrorCodes.ClassIndexDuplicated or
            ErrorCodes.ClassBulkCreateInvalid or
            ErrorCodes.ClassLecturerRequired or
            ErrorCodes.ClassCompleted or
            ErrorCodes.ClassCompletionBlocked or
            ErrorCodes.ClassArchived or
            ErrorCodes.ClassStudentIdentityConflict or
            ErrorCodes.ClassStudentMajorMismatch or
            ErrorCodes.ClassStudentAlreadyEnrolled or
            ErrorCodes.ClassStudentIsTeamLeader or
            ErrorCodes.ClassStudentInActiveTeam or
            ErrorCodes.ClassStudentEnrollmentConflict or
            ErrorCodes.ClassStudentReEnrollmentRequired or
            ErrorCodes.ClassStudentNotDropped or
            ErrorCodes.TeamMembershipConflict or
            ErrorCodes.ClassEnrollmentMajorLocked or
            ErrorCodes.ClassImportSessionInvalid or
            ErrorCodes.ClassImportSessionExpired or
            ErrorCodes.ClassImportSessionAlreadyProcessing => Conflict(response),
            ErrorCodes.ClassRestoreInvalid => Conflict(response),
            _ => BadRequest(response)
        };
    }

    private async Task<IActionResult> SetEnrollmentMajorLock(
        Guid id,
        bool shouldLock,
        ISetEnrollmentMajorLockCommandHandler commandHandler,
        CancellationToken cancellationToken)
    {
        var result = await commandHandler.HandleAsync(
            id,
            shouldLock,
            _currentUserService.UserId ?? Guid.Empty,
            GetCurrentUserRole(),
            cancellationToken);
        if (result.IsFailure)
        {
            return ToClassErrorResponse(result.Error);
        }

        return Ok(ApiResponse<EnrollmentMajorLockResponse>.SuccessResponse(
            result.Value,
            shouldLock ? "Enrollment majors locked." : "Enrollment majors unlocked."));
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
