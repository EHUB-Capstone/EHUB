using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using EHub.Infrastructure.Identity;
using EHub.Shared.Constants;

namespace EHub.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

        if (jwtOptions == null)
        {
            throw new InvalidOperationException("JWT configuration section is missing.");
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
        {
            throw new InvalidOperationException("JWT Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
        {
            throw new InvalidOperationException("JWT Audience is required.");
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
        {
            throw new InvalidOperationException("JWT Secret is required.");
        }

        if (jwtOptions.Secret.Length < 32)
        {
            throw new InvalidOperationException("JWT Secret key must be at least 32 characters long.");
        }

        if (jwtOptions.AccessTokenExpirationMinutes <= 0)
        {
            throw new InvalidOperationException("JWT AccessTokenExpirationMinutes must be greater than 0.");
        }

        if (jwtOptions.RefreshTokenExpirationDays <= 0)
        {
            throw new InvalidOperationException("JWT RefreshTokenExpirationDays must be greater than 0.");
        }

        // Configure JWT Bearer Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = ClaimNames.UserId,
                RoleClaimType = ClaimNames.Role
            };

            // Diagnostic: log JWT validation failures
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    Console.WriteLine($"[JWT] Authentication FAILED: {context.Exception.GetType().Name}: {context.Exception.Message}");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    Console.WriteLine($"[JWT] Token VALIDATED for: {context.Principal?.Identity?.Name}");
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    Console.WriteLine($"[JWT] Challenge issued. Error: {context.Error}, Desc: {context.ErrorDescription}");
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }
}
