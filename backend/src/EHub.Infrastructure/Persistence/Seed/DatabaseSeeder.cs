using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using EHub.Application.Common.Interfaces.Identity;

namespace EHub.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAllAsync(
        AppDbContext context,
        IConfiguration configuration,
        IPasswordHasher passwordHasher)
    {
        await RoleSeeder.SeedAsync(context);
        await SemesterSeeder.SeedAsync(context);
        await CourseSeeder.SeedAsync(context);
        await CheckpointSeeder.SeedAsync(context);
        await RubricSeeder.SeedAsync(context);
        await MentorSeeder.SeedAsync(context);
        await DataBankColumnSeeder.SeedAsync(context);
        await AdminUserSeeder.SeedAsync(context, configuration, passwordHasher);
    }
}
