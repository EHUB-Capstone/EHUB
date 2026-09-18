using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.ProductFeedback;
using EHub.Contracts.Common;
using EHub.Contracts.ProductFeedback;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController, Authorize, Route("api/product-feedback")]
public sealed class ProductFeedbackController(ICurrentUserService current) : ControllerBase
{
    Guid U => current.UserId ?? Guid.Empty;
    string R => current.Roles.FirstOrDefault(x => x.Equals(SystemRoles.Admin, StringComparison.OrdinalIgnoreCase)) ?? current.Roles.FirstOrDefault(x => x.Equals(SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase)) ?? current.Roles.FirstOrDefault(x => x.Equals(SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase)) ?? current.Roles.FirstOrDefault(x => x.Equals(SystemRoles.Student, StringComparison.OrdinalIgnoreCase)) ?? "";
    bool IsAdmin => R == SystemRoles.Admin;

    [HttpPost] public async Task<IActionResult> Create(CreateProductFeedbackRequest request, [FromServices] IProductFeedbackHandler handler, CancellationToken ct) => Out(await handler.CreateAsync(request, U, R, ct), "Thank you. Your feedback has been recorded.");
    [HttpGet("mine")] public async Task<IActionResult> Mine([FromServices] IProductFeedbackHandler handler, CancellationToken ct) => Out(await handler.MineAsync(U, ct), "Feedback retrieved.");
    [HttpGet] public async Task<IActionResult> Inbox([FromQuery] string? category, [FromQuery] string? role, [FromQuery] string? from, [FromQuery] string? to, [FromServices] IProductFeedbackHandler handler, CancellationToken ct) => !IsAdmin ? Forbid() : Out(await handler.InboxAsync(category, role, from, to, ct), "Feedback inbox retrieved.");
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, [FromServices] IProductFeedbackHandler handler, CancellationToken ct) => Out(await handler.GetAsync(id, U, R, ct), "Feedback retrieved.");
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, [FromServices] IProductFeedbackHandler handler, CancellationToken ct) => !IsAdmin ? Forbid() : Out(await handler.DeleteAsync(id, ct), "Feedback deleted.");
    [HttpDelete] public async Task<IActionResult> DeleteAll([FromServices] IProductFeedbackHandler handler, CancellationToken ct) => !IsAdmin ? Forbid() : Out(await handler.DeleteAllAsync(ct), "All feedback deleted.");
    [HttpPost("{id:guid}/attachments"), RequestSizeLimit(10 * 1024 * 1024)] public async Task<IActionResult> Upload(Guid id, IFormFile file, [FromServices] IProductFeedbackHandler handler, CancellationToken ct) { if (file is null) return BadRequest(); await using var stream = file.OpenReadStream(); return Out(await handler.UploadAsync(id, stream, file.FileName, file.Length, U, R, ct), "Attachment uploaded."); }
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")] public async Task<IActionResult> Download(Guid id, Guid attachmentId, [FromServices] IProductFeedbackHandler handler, CancellationToken ct) { var result = await handler.DownloadAsync(id, attachmentId, U, R, ct); return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType) : Err(result.Error); }
    IActionResult Out<T>(Result<T> result, string message) => result.IsSuccess ? Ok(ApiResponse<T>.SuccessResponse(result.Value!, message)) : Err(result.Error);
    IActionResult Out(Result result, string message) => result.IsSuccess ? Ok(ApiResponse<object?>.SuccessResponse(null, message)) : Err(result.Error);
    IActionResult Err(Error error) => error.Code == ErrorCodes.ProductFeedbackAccessDenied ? StatusCode(403, ApiResponse<object>.FailureResponse(error.Message, error.Code)) : error.Code == ErrorCodes.ProductFeedbackNotFound ? NotFound(ApiResponse<object>.FailureResponse(error.Message, error.Code)) : BadRequest(ApiResponse<object>.FailureResponse(error.Message, error.Code));
}
