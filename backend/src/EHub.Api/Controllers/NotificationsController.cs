using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Notifications.GetNotifications;
using EHub.Application.Features.Notifications.MarkNotificationRead;
using EHub.Contracts.Common;
using EHub.Contracts.Notifications;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromServices] IGetNotificationsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(UserId, cancellationToken);

        return ToResponse(result, "Notifications retrieved.");
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        [FromServices] IGetNotificationsQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetUnreadCountAsync(UserId, cancellationToken);

        return ToResponse(result, "Unread notification count retrieved.");
    }

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid notificationId,
        [FromServices] IMarkNotificationReadCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.MarkReadAsync(
            notificationId,
            UserId,
            cancellationToken);

        return ToResponse(result, "Notification marked as read.");
    }

    [HttpPut("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(
        [FromServices] IMarkNotificationReadCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.MarkAllReadAsync(UserId, cancellationToken);

        return ToResponse(result, "Notifications marked as read.");
    }

    private Guid UserId => currentUser.UserId ?? Guid.Empty;

    private IActionResult ToResponse<T>(
        EHub.Shared.Results.Result<T> result,
        string message)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<T>.SuccessResponse(result.Value!, message));
        }

        return ToError(result.Error);
    }

    private IActionResult ToResponse(
        EHub.Shared.Results.Result result,
        string message)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<object?>.SuccessResponse(null, message));
        }

        return ToError(result.Error);
    }

    private IActionResult ToError(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(
            error.Message,
            error.Code);

        return error.Code switch
        {
            ErrorCodes.CommonUnauthorizedError => Unauthorized(response),
            ErrorCodes.NotificationNotFound => NotFound(response),
            _ => BadRequest(response)
        };
    }
}
