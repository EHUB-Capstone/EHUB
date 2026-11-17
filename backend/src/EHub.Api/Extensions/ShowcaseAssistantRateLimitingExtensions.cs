using System.Security.Claims;
using System.Threading.RateLimiting;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.RateLimiting;

namespace EHub.Api.Extensions;

public static class ShowcaseAssistantRateLimitPolicies
{
    public const string Chat = "showcase-assistant-chat";
}

public static class ShowcaseAssistantRateLimitingExtensions
{
    /// <summary>Every question costs an AI call, so each signed-in user gets a small per-minute budget.</summary>
    public static IServiceCollection AddShowcaseAssistantRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue<int?>("ShowcaseAssistantRateLimiting:RequestsPerUserPerMinute") is > 0 and var configured ? configured : 8;
        services.Configure<RateLimiterOptions>(options => options.AddPolicy(ShowcaseAssistantRateLimitPolicies.Chat, context =>
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
