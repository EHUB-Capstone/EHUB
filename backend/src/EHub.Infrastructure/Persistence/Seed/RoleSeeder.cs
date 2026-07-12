using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Domain.Entities;
using EHub.Shared.Constants;

namespace EHub.Infrastructure.Persistence.Seed;

public static class RoleSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (!await context.Roles.AnyAsync())
        {
            var roles = new[]
            {
                new Role { Name = SystemRoles.Admin, Description = "System Administrator", CreatedAt = DateTime.UtcNow },
                new Role { Name = SystemRoles.Lecturer, Description = "Class Lecturer / Instructor", CreatedAt = DateTime.UtcNow },
                new Role { Name = SystemRoles.Student, Description = "FPT Student", CreatedAt = DateTime.UtcNow },
                new Role { Name = SystemRoles.Mentor, Description = "Startup Mentor (Business/Technical)", CreatedAt = DateTime.UtcNow }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }
    }
}
