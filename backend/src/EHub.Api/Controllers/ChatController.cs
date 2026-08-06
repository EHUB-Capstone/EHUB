using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Common;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize(Policy = SystemPolicies.AuthenticatedOnly)]
public sealed class ChatController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ChatController(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [HttpGet("groups")]
    public async Task<IActionResult> GetMyChatGroups(CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;
        var currentUserRole = GetCurrentUserRole();

        var currentStudentId = await _context.Students.AsNoTracking()
            .Where(s => s.UserId == currentUserId)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Subquery các ChatGroupId mà user hoặc student record của họ tham gia
        var myGroupIdsQuery = _context.ChatGroupMembers.AsNoTracking()
            .Where(m => m.IsActive &&
                ((m.UserId.HasValue && m.UserId == currentUserId) ||
                 (currentStudentId.HasValue && m.StudentId.HasValue && m.StudentId == currentStudentId)))
            .Select(m => m.ChatGroupId);

        // Nếu là Admin, cho phép xem tất cả các chat group của hệ thống nếu chưa nằm trong group
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);

        var groupsQuery = _context.ChatGroups.AsNoTracking();
        if (!isAdmin)
        {
            groupsQuery = groupsQuery.Where(g => myGroupIdsQuery.Contains(g.Id));
        }

        var groups = await groupsQuery
            .OrderBy(g => g.GroupType)
            .ThenBy(g => g.GroupName)
            .Select(g => new
            {
                _id = g.Id,
                id = g.Id,
                groupName = g.GroupName,
                groupType = g.GroupType.ToString(),
                classId = g.ClassId,
                teamId = g.TeamId,
                isReadOnly = g.IsReadOnly,
                unreadCount = 0
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResponse(groups, "Chat channels retrieved successfully."));
    }

    [HttpGet("groups/{chatGroupId:guid}/members")]
    public async Task<IActionResult> GetChatGroupMembers(Guid chatGroupId, CancellationToken cancellationToken)
    {
        var group = await _context.ChatGroups.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == chatGroupId, cancellationToken);

        if (group == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Chat group was not found.", ErrorCodes.ClassNotFound));
        }

        var members = await _context.ChatGroupMembers.AsNoTracking()
            .Where(m => m.ChatGroupId == chatGroupId && m.IsActive)
            .Select(m => new
            {
                _id = m.Id,
                id = m.Id,
                chatGroupId = m.ChatGroupId,
                userId = m.UserId ?? (m.Student != null ? m.Student.UserId : (Guid?)null),
                studentId = m.StudentId,
                displayName = m.User != null ? m.User.FullName : (m.Student != null ? m.Student.FullName : "Member"),
                email = m.User != null ? m.User.Email : (m.Student != null ? m.Student.Email : string.Empty),
                role = m.Role.ToString().ToUpperInvariant(),
                nickname = m.Nickname
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResponse(new { members }, "Chat members retrieved successfully."));
    }

    [HttpGet("groups/{chatGroupId:guid}/messages")]
    public async Task<IActionResult> GetChatGroupMessages(Guid chatGroupId, CancellationToken cancellationToken)
    {
        var groupExists = await _context.ChatGroups.AsNoTracking()
            .AnyAsync(g => g.Id == chatGroupId, cancellationToken);

        if (!groupExists)
        {
            return NotFound(ApiResponse<object>.FailureResponse("Chat group was not found.", ErrorCodes.ClassNotFound));
        }

        var messages = await _context.ChatMessages.AsNoTracking()
            .Where(m => m.ChatGroupId == chatGroupId && !m.IsRevoked)
            .OrderBy(m => m.CreatedAt)
            .Take(200)
            .Select(m => new
            {
                _id = m.Id,
                id = m.Id,
                chatGroupId = m.ChatGroupId,
                sender = new
                {
                    _id = m.SenderUserId,
                    id = m.SenderUserId,
                    name = m.SenderName,
                    role = m.SenderRole
                },
                content = m.Text ?? string.Empty,
                messageType = m.MessageType.ToString(),
                attachmentJson = m.AttachmentJson,
                reactionsJson = m.ReactionsJson,
                isEdited = m.IsEdited,
                createdAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResponse(messages, "Chat messages retrieved successfully."));
    }

    [HttpPatch("groups/{chatGroupId:guid}/nickname")]
    public async Task<IActionResult> UpdateMemberNickname(
        Guid chatGroupId,
        [FromBody] UpdateNicknameRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        var member = await _context.ChatGroupMembers
            .FirstOrDefaultAsync(m => m.ChatGroupId == chatGroupId && m.UserId == currentUserId && m.IsActive, cancellationToken);

        if (member == null)
        {
            return NotFound(ApiResponse<object>.FailureResponse("You are not an active member of this chat group.", ErrorCodes.ClassAccessDenied));
        }

        member.Nickname = request.Nickname?.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.SuccessResponse(
            new { memberId = member.Id, nickname = member.Nickname },
            "Nickname updated successfully."));
    }

    private string GetCurrentUserRole()
    {
        return User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value
            ?? User.Claims.FirstOrDefault(c => c.Type == "role")?.Value
            ?? string.Empty;
    }
}

public sealed record UpdateNicknameRequest(string? Nickname);
