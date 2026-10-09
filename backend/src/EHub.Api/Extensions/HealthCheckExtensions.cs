using System.Linq;
using System.Text.Json;
using EHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EHub.Api.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: ["live"])
            .AddDbContextCheck<AppDbContext>(
                "postgresql",
                tags: ["ready"]);

        return services;
    }

    public static IEndpointRouteBuilder MapApplicationHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", CreateOptions(
            registration => registration.Tags.Contains("live")));

        endpoints.MapHealthChecks("/health/ready", CreateOptions(
            _ => true));

        // Backwards-compatible aggregate endpoint.
        endpoints.MapHealthChecks("/health", CreateOptions(
            _ => true));

        return endpoints;
    }

    private static HealthCheckOptions CreateOptions(
        Func<HealthCheckRegistration, bool> predicate) =>
        new()
        {
            Predicate = predicate,
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";

                // Map status code appropriately (200 for Healthy, 503 for Unhealthy)
                context.Response.StatusCode = report.Status == HealthStatus.Healthy
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable;

                var response = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description,
                        duration = entry.Value.Duration.ToString()
                    }),
                    totalDuration = report.TotalDuration.ToString()
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        };
}
