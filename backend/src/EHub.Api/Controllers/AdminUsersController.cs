using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EHub.Application.Features.Admin.Users.ApproveUser;
using EHub.Application.Features.Admin.Users.GetPendingApprovalUsers;
using EHub.Application.Features.Admin.Users.RejectUser;
using EHub.Contracts.Admin.Users;
using EHub.Contracts.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = SystemPolicies.AdminOnly)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IGetPendingApprovalUsersQueryHandler _getPendingApprovalUsersQueryHandler;
    private readonly IApproveUserCommandHandler _approveUserCommandHandler;
    private readonly IRejectUserCommandHandler _rejectUserCommandHandler;

    public AdminUsersController(
        IGetPendingApprovalUsersQueryHandler getPendingApprovalUsersQueryHandler,
        IApproveUserCommandHandler approveUserCommandHandler,
        IRejectUserCommandHandler rejectUserCommandHandler)
    {
        _getPendingApprovalUsersQueryHandler = getPendingApprovalUsersQueryHandler;
        _approveUserCommandHandler = approveUserCommandHandler;
        _rejectUserCommandHandler = rejectUserCommandHandler;
    }

    [HttpGet("pending-approval")]
    public async Task<IActionResult> GetPendingApprovalUsers(
        CancellationToken cancellationToken)
    {
        var result = await _getPendingApprovalUsersQueryHandler.HandleAsync(
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(
                result.Error.Message,
                result.Error.Code));
        }

        return Ok(ApiResponse<IReadOnlyCollection<PendingApprovalUserResponse>>.SuccessResponse(
            result.Value,
            "Pending approval users retrieved successfully."));
    }

    [HttpPost("{userId:guid}/approve")]
    public async Task<IActionResult> ApproveUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await _approveUserCommandHandler.HandleAsync(
            userId,
            cancellationToken);

        if (result.IsFailure)
        {
            return MapAdminUserError(result.Error);
        }

        return Ok(ApiResponse<object?>.SuccessResponse(
            null,
            "User approved successfully."));
    }

    [HttpPost("{userId:guid}/reject")]
    public async Task<IActionResult> RejectUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await _rejectUserCommandHandler.HandleAsync(
            userId,
            cancellationToken);

        if (result.IsFailure)
        {
            return MapAdminUserError(result.Error);
        }

        return Ok(ApiResponse<object?>.SuccessResponse(
            null,
            "User rejected successfully."));
    }

    private IActionResult MapAdminUserError(Error error)
    {
        return error.Code switch
        {
            ErrorCodes.UserNotFound => NotFound(
                ApiResponse<object>.FailureResponse(
                    error.Message,
                    error.Code)),

            ErrorCodes.ApprovalUserNotPending => Conflict(
                ApiResponse<object>.FailureResponse(
                    error.Message,
                    error.Code)),

            ErrorCodes.ApprovalInvalidTargetRole => BadRequest(
                ApiResponse<object>.FailureResponse(
                    error.Message,
                    error.Code)),

            ErrorCodes.ApprovalEmailNotVerified => Conflict(
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
