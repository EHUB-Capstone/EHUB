using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace EHub.Api.Extensions;

public static class SwaggerExtensions
{
    private static readonly HashSet<string> DuplicateTypeNames = typeof(SwaggerExtensions).Assembly
        .GetReferencedAssemblies()
        .Where(name => name.Name!.StartsWith("EHub.", StringComparison.Ordinal))
        .Select(Assembly.Load)
        .Append(typeof(SwaggerExtensions).Assembly)
        .SelectMany(assembly => assembly.GetExportedTypes())
        .GroupBy(type => type.Name)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToHashSet(StringComparer.Ordinal);

    private static string SchemaId(Type type, HashSet<string> duplicates)
    {
        if (type.IsGenericType)
        {
            var baseName = type.Name[..type.Name.IndexOf('`')];
            return baseName + string.Concat(type.GetGenericArguments().Select(arg => SchemaId(arg, duplicates)));
        }

        if (!duplicates.Contains(type.Name) || type.Namespace is null)
            return type.Name;

        var lastSegment = type.Namespace[(type.Namespace.LastIndexOf('.') + 1)..];
        return lastSegment + type.Name;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "E-HUB API",
                Version = "v1",
                Description = "Backend API for Entrepreneurship Hub system."
            });

            // Two contracts can share a class name across namespaces (e.g. UpdateMentorProfileRequest);
            // qualify only those with their namespace so Swashbuckle does not throw on duplicate schemaIds.
            options.CustomSchemaIds(type => SchemaId(type, DuplicateTypeNames));

            // Security scheme: API Key (header) approach
            // This is more reliable across Swashbuckle/Swagger UI versions than Http Bearer
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Enter the full Authorization header value: Bearer {your JWT token}",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            };

            options.AddSecurityDefinition("Bearer", securityScheme);

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", document),
                    new List<string>()
                }
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
    {
        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "E-HUB API v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}
