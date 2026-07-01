using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.Api.Extensions;

public static class CorsExtensions
{
    public const string FrontendPolicy = "FrontendPolicy";

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>();

        if (allowedOrigins is null || allowedOrigins.Length == 0)
        {
            var originString = configuration["Cors:AllowedOrigins"];

            allowedOrigins = string.IsNullOrWhiteSpace(originString)
                ? new[] { "http://localhost:5173" }
                : originString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendPolicy, policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }
}
