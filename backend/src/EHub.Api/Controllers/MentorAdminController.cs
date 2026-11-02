using EHub.Application.Features.Admin.Mentors;
using EHub.Application.Features.Admin.Mentors.ExportAssignments;
using EHub.Api.Models.Common;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/admin/mentors")]
[Authorize(Policy = SystemPolicies.AdminOnly)]
public sealed class MentorAdminController(IMentorAdminHandler handler, IMentorAssignmentExportHandler exportHandler) : ControllerBase
{
    [HttpGet("import-template")]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var result = await handler.GetTemplateAsync(cancellationToken);
        return result.IsSuccess
            ? File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName)
            : ToError(result.Error);
    }

    // Imports mentors into the master list: accounts and profiles only. Confirm with imports/commit.
    [HttpPost("master-imports/preview")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PreviewMasterImport(
    [FromForm] FileUploadForm request,
    CancellationToken cancellationToken) =>
    ToResponse(
        await handler.PreviewImportAsync(request.File, cancellationToken),
        "Mentor master list import preview generated successfully.");

    // Mentors kept in the master list without a login account yet.
    [HttpGet("incomplete")]
    public async Task<IActionResult> GetIncompleteMasterMentors(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? mentorType = null,
        CancellationToken cancellationToken = default) =>
        ToResponse(await handler.GetIncompleteMasterMentorsAsync(page, limit, search, mentorType, cancellationToken), "Incomplete mentors retrieved successfully.");

    [HttpPut("incomplete/{id:guid}")]
    public async Task<IActionResult> UpdateIncompleteMentor(Guid id, [FromBody] UpdateIncompleteMentorRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateIncompleteMentorAsync(id, request, cancellationToken), "Mentor updated successfully.");

    [HttpDelete("incomplete/{id:guid}")]
    public async Task<IActionResult> DeleteIncompleteMentor(Guid id, CancellationToken cancellationToken)
    {
        var result = await handler.DeleteIncompleteMentorAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(ApiResponse<object?>.SuccessResponse(null, "Mentor deleted successfully.")) : ToError(result.Error);
    }

    [HttpPost("imports/commit")]
    public async Task<IActionResult> CommitImport([FromBody] CommitMentorImportRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.CommitImportAsync(request, cancellationToken), "Mentors imported successfully.");

    // Active classes of a semester with their open mentor slots, so an admin can pick a class to manage.
    [HttpGet("semesters/{semesterId:guid}/classes")]
    public async Task<IActionResult> GetSemesterClasses(Guid semesterId, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetSemesterClassesAsync(semesterId, cancellationToken), "Semester classes retrieved successfully.");

    [HttpPost("allocations/preview")]
    public async Task<IActionResult> PreviewAllocation([FromBody] PreviewMentorAllocationRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.PreviewAllocationAsync(request, cancellationToken), "Mentor allocation preview generated successfully.");

    [HttpPost("allocations/commit")]
    public async Task<IActionResult> CommitAllocation([FromBody] CommitMentorAllocationRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.CommitAllocationAsync(request, cancellationToken), "Mentor allocation committed successfully.");

    [HttpGet("assignments/export")]
    public async Task<IActionResult> ExportAssignments([FromQuery] Guid semesterId, CancellationToken cancellationToken)
    {
        var result = await exportHandler.ExportAsync(semesterId, cancellationToken);
        return result.IsSuccess
            ? File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName)
            : ToError(result.Error);
    }

    private IActionResult ToResponse<T>(Result<T> result, string message) =>
        result.IsSuccess ? Ok(ApiResponse<T>.SuccessResponse(result.Value, message)) : ToError(result.Error);

    private IActionResult ToError(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);
        if (error.Code == ErrorCodes.CommonUnauthorizedError) return Unauthorized(response);
        if (error.Code == ErrorCodes.MentorMatchingUnavailable) return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        if (error.Code.EndsWith("_NOT_FOUND", StringComparison.OrdinalIgnoreCase)) return NotFound(response);
        if (error.Code.Contains("SESSION", StringComparison.OrdinalIgnoreCase) || error.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
            return Conflict(response);
        return BadRequest(response);
    }
}
