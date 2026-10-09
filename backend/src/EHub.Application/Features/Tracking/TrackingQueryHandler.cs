using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Dashboard;
using EHub.Shared.Results;
using EHub.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Tracking;

public sealed class TrackingQueryHandler(IApplicationDbContext context) : ITrackingQueryHandler
{
    public async Task<Result<AuthStatsResponse>> GetAuthStatsAsync(int days, CancellationToken cancellationToken = default)
    {
        if (days is not (7 or 30)) return Result.Failure<AuthStatsResponse>(new Error("VALIDATION_ERROR", "Days must be 7 or 30."));
        var today = DateTime.UtcNow.Date; var start = today.AddDays(-(days - 1));
        var registered = await context.Users.AsNoTracking().Where(user => user.CreatedAt >= start).GroupBy(user => user.CreatedAt.Date).Select(group => new { Date = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        var logins = await context.Users.AsNoTracking().Where(user => user.LastLoginAt != null && user.LastLoginAt >= start).GroupBy(user => user.LastLoginAt!.Value.Date).Select(group => new { Date = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        var registerByDate = registered.ToDictionary(item => item.Date, item => item.Count); var loginByDate = logins.ToDictionary(item => item.Date, item => item.Count);
        var dates = Enumerable.Range(0, days).Select(offset => start.AddDays(offset)).ToArray();
        return Result.Success(new AuthStatsResponse { TotalUsers = await context.Users.CountAsync(cancellationToken), TotalRegisters = registered.Sum(item => item.Count), TotalLogins = logins.Sum(item => item.Count), FailedLogins = 0, TodayRegisters = registerByDate.GetValueOrDefault(today), TodayLogins = loginByDate.GetValueOrDefault(today), ActiveUsersToday = loginByDate.GetValueOrDefault(today), RegisterRate = dates.Select(date => new DateCountResponse { Date = date.ToString("yyyy-MM-dd"), Count = registerByDate.GetValueOrDefault(date) }).ToArray(), LoginRate = dates.Select(date => new DateCountResponse { Date = date.ToString("yyyy-MM-dd"), Count = loginByDate.GetValueOrDefault(date) }).ToArray() });
    }
    public async Task<Result<OnlineUsersResponse>> GetOnlineUsersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var users = await context.Users.AsNoTracking().Where(user => user.LastLoginAt != null).OrderByDescending(user => user.LastLoginAt).Select(user => new ActivityUserResponse { Id = user.Id, Name = user.FullName, Email = user.Email, Avatar = user.AvatarUrl, Role = user.UserRoles.Select(role => role.Role.Name).FirstOrDefault() ?? "STUDENT", LastSeen = user.LastLoginAt }).Take(24).ToArrayAsync(cancellationToken);
        var online = users.Where(user => user.LastSeen >= now.AddMinutes(-5)).ToArray();
        return Result.Success(new OnlineUsersResponse { OnlineCount = online.Length, TotalUsers = await context.Users.CountAsync(cancellationToken), OnlineUsers = online, RecentlyActive = users.Where(user => user.LastSeen < now.AddMinutes(-5)).Take(12).ToArray() });
    }
}
