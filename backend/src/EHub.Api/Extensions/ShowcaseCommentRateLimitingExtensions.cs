using System.Security.Claims;
using System.Threading.RateLimiting;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.RateLimiting;

namespace EHub.Api.Extensions;

public static class ShowcaseCommentRateLimitPolicies
{
    public const string Create = "showcase-comment-create";
}

public static class ShowcaseCommentRateLimitingExtensions
{
    public static IServiceCollection AddShowcaseCommentRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue<int?>("ShowcaseCommentRateLimiting:RequestsPerUserPerMinute") is > 0 and var configured ? configured : 10;
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(ShowcaseCommentRateLimitPolicies.Create, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(ClaimNames.UserId)?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
        return services;
    }
}
