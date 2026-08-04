using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Common;
using EHub.Contracts.Dashboard;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/tracking")]
[Authorize(Policy = SystemPolicies.AdminOnly)]
public sealed class TrackingController(IApplicationDbContext context) : ControllerBase
{
    [HttpGet("auth-stats")]
    public async Task<IActionResult> GetAuthStats([FromQuery] int days = 7, CancellationToken cancellationToken = default)
    {
        if (days is not (7 or 30)) return BadRequest(ApiResponse<object>.FailureResponse("Days must be 7 or 30.", "VALIDATION_ERROR"));
        var today = DateTime.UtcNow.Date;
        var start = today.AddDays(-(days - 1));
        var registered = await context.Users.AsNoTracking().Where(user => user.CreatedAt >= start).GroupBy(user => user.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        var logins = await context.Users.AsNoTracking().Where(user => user.LastLoginAt != null && user.LastLoginAt >= start).GroupBy(user => user.LastLoginAt!.Value.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        var registerByDate = registered.ToDictionary(item => item.Date, item => item.Count);
        var loginByDate = logins.ToDictionary(item => item.Date, item => item.Count);
        var dates = Enumerable.Range(0, days).Select(offset => start.AddDays(offset)).ToArray();
        var response = new AuthStatsResponse
        {
            TotalUsers = await context.Users.CountAsync(cancellationToken),
            TotalRegisters = registered.Sum(item => item.Count),
            TotalLogins = logins.Sum(item => item.Count),
            FailedLogins = 0,
            TodayRegisters = registerByDate.GetValueOrDefault(today),
            TodayLogins = loginByDate.GetValueOrDefault(today),
            ActiveUsersToday = loginByDate.GetValueOrDefault(today),
            RegisterRate = dates.Select(date => new DateCountResponse { Date = date.ToString("yyyy-MM-dd"), Count = registerByDate.GetValueOrDefault(date) }).ToArray(),
            LoginRate = dates.Select(date => new DateCountResponse { Date = date.ToString("yyyy-MM-dd"), Count = loginByDate.GetValueOrDefault(date) }).ToArray(),
        };
        return Ok(ApiResponse<AuthStatsResponse>.SuccessResponse(response, "Authentication statistics retrieved successfully."));
    }

    [HttpGet("online-users")]
    public async Task<IActionResult> GetOnlineUsers(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var users = await context.Users.AsNoTracking().Where(user => user.LastLoginAt != null).OrderByDescending(user => user.LastLoginAt)
            .Select(user => new ActivityUserResponse { Id = user.Id, Name = user.FullName, Email = user.Email, Avatar = user.AvatarUrl, Role = user.UserRoles.Select(role => role.Role.Name).FirstOrDefault() ?? "STUDENT", LastSeen = user.LastLoginAt })
            .Take(24).ToArrayAsync(cancellationToken);
        var online = users.Where(user => user.LastSeen >= now.AddMinutes(-5)).ToArray();
        var response = new OnlineUsersResponse
        {
            OnlineCount = online.Length,
            TotalUsers = await context.Users.CountAsync(cancellationToken),
            OnlineUsers = online,
            RecentlyActive = users.Where(user => user.LastSeen < now.AddMinutes(-5)).Take(12).ToArray(),
        };
        return Ok(ApiResponse<OnlineUsersResponse>.SuccessResponse(response, "Online users retrieved successfully."));
    }
}
