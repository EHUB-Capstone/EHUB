using EHub.Api.Extensions;
using EHub.Infrastructure;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddInfrastructure(builder.Configuration);

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
    try
    {
        await context.Database.MigrateAsync();
        await DatabaseSeeder.SeedAllAsync(context);
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

// app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health Check Endpoint
app.MapApplicationHealthChecks();

app.Run();
