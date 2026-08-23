using System.Threading.RateLimiting;
using EHub.Contracts.Common;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace EHub.Api.Extensions;

public static class AuthRateLimitPolicies
{
    public const string Registration = "auth-registration";
    public const string OtpVerification = "auth-otp-verification";
    public const string OtpResend = "auth-otp-resend";
}

public static class AuthRateLimitingExtensions
{
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfterSeconds = context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    : 60;
                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse<object>.FailureResponse(
                        "Too many verification requests. Please try again later.",
                        ErrorCodes.AuthVerificationRateLimited),
                    cancellationToken);
            };

            // Per-registration limits in the application layer remain the
            // primary brute-force control. These IP limits absorb abuse while
            // allowing legitimate users behind a shared campus network.
            AddFixedWindowPolicy(options, AuthRateLimitPolicies.Registration, 30, TimeSpan.FromMinutes(15));
            AddFixedWindowPolicy(options, AuthRateLimitPolicies.OtpVerification, 100, TimeSpan.FromMinutes(5));
            AddFixedWindowPolicy(options, AuthRateLimitPolicies.OtpResend, 60, TimeSpan.FromHours(1));
        });

        return services;
    }

    private static void AddFixedWindowPolicy(
        RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        options.AddPolicy(policyName, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    }
}
