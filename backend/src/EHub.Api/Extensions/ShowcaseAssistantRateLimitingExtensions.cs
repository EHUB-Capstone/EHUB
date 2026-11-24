using System.Security.Claims;
using System.Threading.RateLimiting;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.RateLimiting;

namespace EHub.Api.Extensions;

public static class ShowcaseAssistantRateLimitPolicies
{
    public const string Chat = "showcase-assistant-chat";
    public const string Key = "showcase-assistant-key";
}

public static class ShowcaseAssistantRateLimitingExtensions
{
    /// <summary>
    /// Every question costs an AI call, so each signed-in user gets a small per-minute budget. Looking at, saving or removing the
    /// stored key is cheap but still limited, so it cannot be hammered and it does not eat into the question budget.
    /// </summary>
    public static IServiceCollection AddShowcaseAssistantRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var chatLimit = configuration.GetValue<int?>("ShowcaseAssistantRateLimiting:RequestsPerUserPerMinute") is > 0 and var configured ? configured : 8;
        var keyLimit = configuration.GetValue<int?>("ShowcaseAssistantRateLimiting:KeyRequestsPerUserPerMinute") is > 0 and var configuredKey ? configuredKey : 30;
        services.Configure<RateLimiterOptions>(options =>
        {
            options.AddPolicy(ShowcaseAssistantRateLimitPolicies.Chat, context => PerUser(context, chatLimit));
            options.AddPolicy(ShowcaseAssistantRateLimitPolicies.Key, context => PerUser(context, keyLimit));
        });
        return services;
    }

    private static RateLimitPartition<string> PerUser(HttpContext context, int permitLimit) =>
        context.User.Identity?.IsAuthenticated != true
            ? RateLimitPartition.GetNoLimiter<string>("anonymous")
            : RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(ClaimNames.UserId)?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                });
}
