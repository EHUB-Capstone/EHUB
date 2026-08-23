using System.Linq;
using System.Text.Json;
using EHub.Api.Extensions;
using EHub.Api.Filters;
using EHub.Application;
using EHub.Contracts.Common;
using EHub.Infrastructure;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.ValidateRuntimeConfiguration(builder.Environment);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddApplicationAuthorization();
builder.Services.AddAuthRateLimiting();

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
builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);
builder.Services.AddApplicationHealthChecks();

if (builder.Configuration.GetValue<bool>("ReverseProxy:Enabled"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        // Render terminates public TLS at its managed reverse proxy. Only
        // enable this setting when the app is reachable exclusively through a
        // trusted hosting reverse proxy, as it is in the staging topology.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("ReverseProxy:Enabled"))
{
    // Must run before HTTPS redirection, authentication, and URL generation.
    app.UseForwardedHeaders();
}

// Global Exception Handling at the beginning
app.UseGlobalExceptionHandling();

if (DatabaseInitializationExtensions.IsInitializationRequested(args))
{
    await app.Services.InitializeDatabaseAsync();
    return;
}

// Auto-migrate and seed in Development environment
if (app.Environment.IsDevelopment())
{
    await app.Services.InitializeDatabaseAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation(); // Swagger UI enabled in Development
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();

// CORS must be configured before Authentication & Authorization middleware
app.UseCors(CorsExtensions.FrontendPolicy);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health Check Endpoint
app.MapApplicationHealthChecks();

try
{
    Log.Information("Starting web host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
