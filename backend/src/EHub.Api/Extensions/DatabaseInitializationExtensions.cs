using EHub.Application.Common.Interfaces.Identity;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace EHub.Api.Extensions;

public static class DatabaseInitializationExtensions
{
    public const string InitializationArgument = "--initialize-database";

    public static bool IsInitializationRequested(IEnumerable<string> arguments) =>
        arguments.Any(argument =>
            string.Equals(
                argument,
                InitializationArgument,
                StringComparison.OrdinalIgnoreCase));

    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var serviceProvider = scope.ServiceProvider;
        var context = serviceProvider.GetRequiredService<AppDbContext>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
        var logger = serviceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitialization");

        try
        {
            logger.LogInformation("Applying database migrations");
            await context.Database.MigrateAsync(cancellationToken);

            logger.LogInformation("Applying idempotent reference-data seeders");
            await DatabaseSeeder.SeedAllAsync(context, configuration, passwordHasher);

            logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception exception)
        {
            logger.LogCritical(
                exception,
                "Database initialization failed. The application will not continue with a partially initialized schema");
            throw;
        }
    }
}
