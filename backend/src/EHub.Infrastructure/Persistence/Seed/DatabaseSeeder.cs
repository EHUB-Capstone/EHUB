using System.Threading.Tasks;

namespace EHub.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAllAsync(AppDbContext context)
    {
        await RoleSeeder.SeedAsync(context);
        await SemesterSeeder.SeedAsync(context);
        await CourseSeeder.SeedAsync(context);
        await CheckpointSeeder.SeedAsync(context);
    }
}
