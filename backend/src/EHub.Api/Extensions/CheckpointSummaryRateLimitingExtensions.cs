using System.Security.Claims;
using System.Threading.RateLimiting;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.RateLimiting;

namespace EHub.Api.Extensions;

public static class CheckpointSummaryRateLimitPolicies
{
    public const string Request = "checkpoint-summary-request";
}

public static class CheckpointSummaryRateLimitingExtensions
{
    /// <summary>
    /// Limits how often one reviewer can queue AI summaries. Partitioned by user rather than IP because staff share
    /// campus networks. The rejection response comes from the shared limiter set up in AddAuthRateLimiting.
    /// </summary>
    public static IServiceCollection AddCheckpointSummaryRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue<int?>("Summarization:RequestsPerUserPerHour") is > 0 and var configured
            ? configured
            : CheckpointSummaryLimits.RequestsPerUserPerHour;
        services.Configure<RateLimiterOptions>(options =>
            options.AddPolicy(CheckpointSummaryRateLimitPolicies.Request, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.User.FindFirst(ClaimNames.UserId)?.Value
                        ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })));
        return services;
    }
}
