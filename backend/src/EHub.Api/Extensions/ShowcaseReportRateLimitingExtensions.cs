using System.Security.Claims;
using System.Threading.RateLimiting;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.RateLimiting;

namespace EHub.Api.Extensions;

public static class ShowcaseReportRateLimitPolicies
{
    public const string Create = "showcase-report-create";
}

public static class ShowcaseReportRateLimitingExtensions
{
    public static IServiceCollection AddShowcaseReportRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue<int?>("ShowcaseReportRateLimiting:RequestsPerUserPerMinute") is > 0 and var configured ? configured : 5;
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(ShowcaseReportRateLimitPolicies.Create, context =>
            context.User.Identity?.IsAuthenticated != true
                ? RateLimitPartition.GetNoLimiter<string>("anonymous")
                : RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirst(ClaimNames.UserId)?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                    })));
        return services;
    }
}
