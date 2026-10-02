using EHub.Application.Features.Admin.Mentors;
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
public sealed class MentorAdminController(IMentorAdminHandler handler) : ControllerBase
{
    [HttpGet("import-template")]
    public async Task<IActionResult> DownloadTemplate(CancellationToken cancellationToken)
    {
        var result = await handler.GetTemplateAsync(cancellationToken);
        return result.IsSuccess
            ? File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName)
            : ToError(result.Error);
    }

    [HttpPost("imports/preview")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PreviewImport([FromForm] Guid semesterId, [FromForm] IFormFile file, CancellationToken cancellationToken) =>
        ToResponse(await handler.PreviewImportAsync(semesterId, file, cancellationToken), "Mentor import preview generated successfully.");

    [HttpPost("imports/commit")]
    public async Task<IActionResult> CommitImport([FromBody] CommitMentorImportRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.CommitImportAsync(request, cancellationToken), "Mentors imported successfully.");

    [HttpPost("allocations/preview")]
    public async Task<IActionResult> PreviewAllocation([FromBody] PreviewMentorAllocationRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.PreviewAllocationAsync(request, cancellationToken), "Balanced mentor allocation preview generated successfully.");

    [HttpPost("allocations/commit")]
    public async Task<IActionResult> CommitAllocation([FromBody] CommitMentorAllocationRequest request, CancellationToken cancellationToken) =>
        ToResponse(await handler.CommitAllocationAsync(request, cancellationToken), "Balanced mentor allocation committed successfully.");

    private IActionResult ToResponse<T>(Result<T> result, string message) =>
        result.IsSuccess ? Ok(ApiResponse<T>.SuccessResponse(result.Value, message)) : ToError(result.Error);

    private IActionResult ToError(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);
        if (error.Code == ErrorCodes.CommonUnauthorizedError) return Unauthorized(response);
        if (error.Code.EndsWith("_NOT_FOUND", StringComparison.OrdinalIgnoreCase)) return NotFound(response);
        if (error.Code.Contains("SESSION", StringComparison.OrdinalIgnoreCase) || error.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
            return Conflict(response);
        return BadRequest(response);
    }
}
