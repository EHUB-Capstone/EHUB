using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using EHub.Application.Common.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.Persistence.Seed;
using EHub.Application.Common.Interfaces.Identity;

namespace EHub.IntegrationTests.Common;

public sealed class CustomWebApplicationFactory 
    : WebApplicationFactory<Program>, Xunit.IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer =
        new PostgreSqlBuilder("postgres:16-alpine")
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
            // Background workers race with explicit outbox assertions and serializable
            // workflow commands. Integration tests invoke those flows directly, so they
            // must run against a deterministic database without hosted workers.
            foreach (var hostedService in services
                         .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                         .ToArray())
            {
                services.Remove(hostedService);
            }

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

            var emailServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailServiceDescriptor != null)
            {
                services.Remove(emailServiceDescriptor);
            }
            services.AddSingleton<IEmailService, FakeEmailService>();
        });
    }
}

public class FakeEmailService : IEmailService
{
    public static string? LastResetUrl { get; set; }
    public static string? LastRawToken { get; set; }
    public static string? LastRegistrationOtp { get; set; }
    public static string? LastRegistrationEmail { get; set; }

    public Task SendRegistrationOtpAsync(
        string toEmail,
        string fullName,
        string otp,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        LastRegistrationEmail = toEmail;
        LastRegistrationOtp = otp;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        LastResetUrl = resetUrl;
        
        if (!string.IsNullOrEmpty(resetUrl))
        {
            try
            {
                var uri = new Uri(resetUrl);
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                if (query.TryGetValue("token", out var tokenValue))
                {
                    LastRawToken = tokenValue;
                }
            }
            catch (UriFormatException)
            {
                // Fallback for relative URI
                var queryStart = resetUrl.IndexOf('?');
                if (queryStart >= 0)
                {
                    var queryStr = resetUrl.Substring(queryStart);
                    var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryStr);
                    if (query.TryGetValue("token", out var tokenValue))
                    {
                        LastRawToken = tokenValue;
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task SendPasswordChangedNotificationAsync(
        string toEmail,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
