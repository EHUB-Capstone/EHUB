using EHub.Api.Models.Common;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Showcase.Manage;
using EHub.Application.Features.Showcase.Moderation;
using EHub.Application.Features.Showcase.Query;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/showcase")]
public sealed class ShowcaseController(ICurrentUserService currentUser) : ControllerBase
{
    private const long MaximumUploadRequestBytes = 6 * 1024 * 1024;

    private Guid UserId => currentUser.UserId ?? Guid.Empty;

    private string Role => currentUser.Roles.FirstOrDefault(role => SystemRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase)) ?? string.Empty;

    [HttpGet("mine")]
    [Authorize(Policy = SystemPolicies.StudentOnly)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetMine(
        [FromServices] IShowcaseManageHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetMineAsync(UserId, Role, cancellationToken), "Showcase pages retrieved.");

    [HttpPost]
    [Authorize(Policy = SystemPolicies.StudentOnly)]
    public async Task<IActionResult> Create(
        [FromBody] SaveShowcaseRequest request,
        [FromServices] IShowcaseManageHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.CreateAsync(request, UserId, Role, cancellationToken), "Showcase draft created.");

    [HttpPut("{id:guid}")]
    [Authorize(Policy = SystemPolicies.StudentOnly)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] SaveShowcaseRequest request,
        [FromServices] IShowcaseManageHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateAsync(id, request, UserId, Role, cancellationToken), "Showcase updated.");

    [HttpPost("{id:guid}/logo")]
    [Authorize(Policy = SystemPolicies.StudentOnly)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadRequestBytes)]
    public async Task<IActionResult> UploadLogo(
        Guid id,
        [FromForm] FileUploadForm request,
        [FromServices] IShowcaseManageHandler handler,
        CancellationToken cancellationToken)
    {
        await using var stream = request.File.OpenReadStream();
        return ToResponse(
            await handler.UploadLogoAsync(id, stream, request.File.FileName, request.File.Length, UserId, Role, cancellationToken),
            "Logo uploaded.");
    }

    [HttpPost("{id:guid}/media")]
    [Authorize(Policy = SystemPolicies.StudentOnly)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadRequestBytes)]
    public async Task<IActionResult> UploadMedia(
        Guid id,
        [FromForm] FileUploadForm request,
        [FromServices] IShowcaseManageHandler handler,
        CancellationToken cancellationToken)
    {
        await using var stream = request.File.OpenReadStream();
        return ToResponse(
            await handler.UploadMediaAsync(id, stream, request.File.FileName, request.File.Length, UserId, Role, cancellationToken),
            "Image uploaded.");
    }

    [HttpDelete("{id:guid}/media/{mediaId:guid}")]
    [Authorize(Policy = SystemPolicies.StudentOnly)]
    public async Task<IActionResult> DeleteMedia(
        Guid id,
        Guid mediaId,
        [FromServices] IShowcaseManageHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.DeleteMediaAsync(id, mediaId, UserId, Role, cancellationToken), "Image removed.");

    [HttpGet("{id:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(
        Guid id,
        [FromServices] IShowcaseQueryHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.GetAsync(id, UserId, Role, cancellationToken), "Showcase retrieved.");

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = SystemPolicies.StudentOnly)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromBody] SubmitShowcaseRequest request,
        [FromServices] IShowcaseModerationHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.SubmitAsync(id, request, UserId, Role, cancellationToken), "Showcase submitted for review.");

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Policy = SystemPolicies.StudentOnly)]
    public async Task<IActionResult> Withdraw(
        Guid id,
        [FromServices] IShowcaseModerationHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.WithdrawAsync(id, UserId, Role, cancellationToken), "Showcase withdrawn.");

    [HttpGet("moderation")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ListForModeration(
        [FromQuery] string? status,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromServices] IShowcaseModerationHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(
            await handler.ListAsync(status, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, UserId, Role, cancellationToken),
            "Showcase review queue retrieved.");

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    public Task<IActionResult> Approve(
        Guid id, [FromBody] ShowcaseDecisionRequest request,
        [FromServices] IShowcaseModerationHandler handler, CancellationToken cancellationToken) =>
        Decide(handler, id, ShowcaseModerationAction.Approve, request, "Showcase approved.", cancellationToken);

    [HttpPost("{id:guid}/request-changes")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    public Task<IActionResult> RequestChanges(
        Guid id, [FromBody] ShowcaseDecisionRequest request,
        [FromServices] IShowcaseModerationHandler handler, CancellationToken cancellationToken) =>
        Decide(handler, id, ShowcaseModerationAction.RequestChanges, request, "Changes requested.", cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    public Task<IActionResult> Reject(
        Guid id, [FromBody] ShowcaseDecisionRequest request,
        [FromServices] IShowcaseModerationHandler handler, CancellationToken cancellationToken) =>
        Decide(handler, id, ShowcaseModerationAction.Reject, request, "Showcase rejected.", cancellationToken);

    [HttpPost("{id:guid}/hide")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    public Task<IActionResult> Hide(
        Guid id, [FromBody] ShowcaseDecisionRequest request,
        [FromServices] IShowcaseModerationHandler handler, CancellationToken cancellationToken) =>
        Decide(handler, id, ShowcaseModerationAction.Hide, request, "Showcase hidden.", cancellationToken);

    [HttpPost("{id:guid}/unhide")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    public Task<IActionResult> Unhide(
        Guid id, [FromBody] ShowcaseDecisionRequest request,
        [FromServices] IShowcaseModerationHandler handler, CancellationToken cancellationToken) =>
        Decide(handler, id, ShowcaseModerationAction.Unhide, request, "Showcase restored.", cancellationToken);

    private async Task<IActionResult> Decide(
        IShowcaseModerationHandler handler, Guid id, ShowcaseModerationAction action,
        ShowcaseDecisionRequest request, string message, CancellationToken cancellationToken) =>
        ToResponse(await handler.DecideAsync(id, action, request.Note, UserId, Role, cancellationToken), message);

    private IActionResult ToResponse<T>(Result<T> result, string message) =>
        result.IsSuccess
            ? Ok(ApiResponse<T>.SuccessResponse(result.Value!, message))
            : ToError(result.Error);

    private IActionResult ToResponse(Result result, string message) =>
        result.IsSuccess
            ? Ok(ApiResponse<object?>.SuccessResponse(null, message))
            : ToError(result.Error);

    private IActionResult ToError(Error error)
    {
        var body = ApiResponse<object>.FailureResponse(error.Message, error.Code);
        return error.Code switch
        {
            ErrorCodes.ShowcaseNotFound => NotFound(body),
            ErrorCodes.ShowcaseAccessDenied => StatusCode(StatusCodes.Status403Forbidden, body),
            ErrorCodes.ShowcaseAlreadyExists or
            ErrorCodes.ShowcaseInvalidState or
            ErrorCodes.ShowcaseConcurrencyConflict => Conflict(body),
            ErrorCodes.ShowcaseImageUploadFailed => StatusCode(StatusCodes.Status502BadGateway, body),
            _ => BadRequest(body)
        };
    }
}
