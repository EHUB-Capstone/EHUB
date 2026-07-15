using System.Linq;
using System.Text.Json;
using EHub.Api.Extensions;
using EHub.Api.Filters;
using EHub.Application;
using EHub.Contracts.Common;
using EHub.Infrastructure;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.Persistence.Seed;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);

// Customize Model State Binding validation response format
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .SelectMany(e => e.Value!.Errors.Select(err => new ValidationError
            {
                Field = ConvertToCamelCase(e.Key),
                Message = err.ErrorMessage,
                Code = "InvalidFormat"
            }));

        var response = ApiResponse<object>.FailureResponse(
            message: "Model validation failed",
            code: ErrorCodes.CommonValidationError,
            errors: errors);

        return new BadRequestObjectResult(response);
    };
});

static string ConvertToCamelCase(string s)
{
    if (string.IsNullOrEmpty(s)) return s;
    var parts = s.Split('.');
    for (int i = 0; i < parts.Length; i++)
    {
        var part = parts[i];
        if (!string.IsNullOrEmpty(part) && char.IsUpper(part[0]))
        {
            parts[i] = char.ToLowerInvariant(part[0]) + part.Substring(1);
        }
    }
    return string.Join(".", parts);
}

// Setup infrastructure extensions
builder.Services.AddSwaggerDocumentation();
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddApplicationHealthChecks();

var app = builder.Build();

// Global Exception Handling at the beginning
app.UseGlobalExceptionHandling();

// Auto-migrate and seed in Development environment
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<EHub.Application.Common.Interfaces.Identity.IPasswordHasher>();
    try
    {
        await context.Database.MigrateAsync();
        await DatabaseSeeder.SeedAllAsync(context, configuration, passwordHasher);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation(); // Swagger UI enabled in Development
}

app.UseHttpsRedirection();

// CORS must be configured before Authentication & Authorization middleware
app.UseCors(CorsExtensions.FrontendPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health Check Endpoint
app.MapApplicationHealthChecks();

app.Run();
