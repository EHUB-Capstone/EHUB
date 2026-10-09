using System.Threading.RateLimiting;
using EHub.Contracts.Common;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace EHub.Api.Extensions;

public static class AuthRateLimitPolicies
{
    public const string Registration = "auth-registration";
    public const string Login = "auth-login";
    public const string GoogleLogin = "auth-google-login";
    public const string ForgotPassword = "auth-forgot-password";
    public const string OtpVerification = "auth-otp-verification";
    public const string OtpResend = "auth-otp-resend";
}

public static class AuthRateLimitingExtensions
{
    public static IServiceCollection AddAuthRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
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
                        "Too many requests. Please try again later.",
                        ErrorCodes.AuthRateLimited),
                    cancellationToken);
            };

            // Per-registration limits in the application layer remain the
            // primary brute-force control. These IP limits absorb abuse while
            // allowing legitimate users behind a shared campus network.
            AddFixedWindowPolicy(options, AuthRateLimitPolicies.Registration, GetPermitLimit(configuration, "Registration", 30), TimeSpan.FromMinutes(15));
            AddFixedWindowPolicy(options, AuthRateLimitPolicies.Login, GetPermitLimit(configuration, "Login", 10), TimeSpan.FromMinutes(5));
            AddFixedWindowPolicy(options, AuthRateLimitPolicies.GoogleLogin, GetPermitLimit(configuration, "GoogleLogin", 20), TimeSpan.FromMinutes(5));
            AddFixedWindowPolicy(options, AuthRateLimitPolicies.ForgotPassword, GetPermitLimit(configuration, "ForgotPassword", 5), TimeSpan.FromMinutes(15));
            AddFixedWindowPolicy(options, AuthRateLimitPolicies.OtpVerification, GetPermitLimit(configuration, "OtpVerification", 100), TimeSpan.FromMinutes(5));
            AddFixedWindowPolicy(options, AuthRateLimitPolicies.OtpResend, GetPermitLimit(configuration, "OtpResend", 60), TimeSpan.FromHours(1));
        });

        return services;
    }

    private static int GetPermitLimit(IConfiguration configuration, string policyName, int defaultValue)
    {
        var configuredValue = configuration.GetValue<int?>($"AuthRateLimiting:{policyName}:PermitLimit");
        return configuredValue is > 0 ? configuredValue.Value : defaultValue;
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
