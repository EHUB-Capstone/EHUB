using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.Persistence.Seed;
using EHub.Application.Common.Interfaces.Identity;

namespace EHub.IntegrationTests.Common;

public sealed class CustomWebApplicationFactory 
    : WebApplicationFactory<Program>, Xunit.IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("ehub_test_db")
            .WithUsername("ehub_test_user")
            .WithPassword("ehub_test_password")
            .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Apply migrations
        await dbContext.Database.MigrateAsync();

        // Seed data
        await DatabaseSeeder.SeedAllAsync(dbContext, configuration, passwordHasher);
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
            });

            var googleAuthDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IGoogleAuthService));
            if (googleAuthDescriptor != null)
            {
                services.Remove(googleAuthDescriptor);
            }
            services.AddScoped<IGoogleAuthService, FakeGoogleAuthService>();
        });
    }
}
