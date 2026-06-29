using System.Threading.Tasks;

namespace EHub.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAllAsync(AppDbContext context)
    {
        await RoleSeeder.SeedAsync(context);
        // Có thể bổ sung thêm các seeders khác sau này (ví dụ: Seed User Admin, Seed Semesters...)
    }
}
