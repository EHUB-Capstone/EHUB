using System.Security.Claims;
using System.Threading.RateLimiting;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.RateLimiting;

namespace EHub.Api.Extensions;

public static class CheckpointSummaryRateLimitPolicies
{
    public const string Request = "checkpoint-summary-request";
    public const string Compare = "checkpoint-summary-compare";

    /// <summary>Administrators' diagnostics page: a light read, so it may be refreshed far more often than a summary can be asked for.</summary>
    public const string Diagnostics = "checkpoint-summary-diagnostics";
}

public static class CheckpointSummaryRateLimitingExtensions
{
    private const int DiagnosticsPerAdminPerHour = 120;

    /// <summary>
    /// Limits how often one reviewer can queue AI summaries. Partitioned by user rather than IP because staff share
    /// campus networks. The rejection response comes from the shared limiter set up in AddAuthRateLimiting.
    /// </summary>
    public static IServiceCollection AddCheckpointSummaryRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue<int?>("Summarization:RequestsPerUserPerHour") is > 0 and var configured
            ? configured
            : CheckpointSummaryLimits.RequestsPerUserPerHour;
        // Comparing versions re-reads two documents, so it is limited like a summary request, but with its own allowance.
        var policies = new[]
        {
            (CheckpointSummaryRateLimitPolicies.Request, permitLimit),
            (CheckpointSummaryRateLimitPolicies.Compare, permitLimit),
            (CheckpointSummaryRateLimitPolicies.Diagnostics, DiagnosticsPerAdminPerHour)
        };
        foreach (var (policy, limit) in policies)
            services.Configure<RateLimiterOptions>(options =>
            options.AddPolicy(policy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.User.FindFirst(ClaimNames.UserId)?.Value
                        ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })));
        return services;
    }
}
