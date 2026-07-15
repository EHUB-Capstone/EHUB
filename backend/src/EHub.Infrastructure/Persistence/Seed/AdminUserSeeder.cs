using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;

namespace EHub.Infrastructure.Persistence.Seed;

public static class AdminUserSeeder
{
    public static async Task SeedAsync(
        AppDbContext context,
        IConfiguration configuration,
        IPasswordHasher passwordHasher)
    {
        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];
        var fullName = configuration["AdminSeed:FullName"] ?? "EHUB Admin";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Fix existing uppercase admin if present from previous runs
        var existingAdminUpper = await context.Users.FirstOrDefaultAsync(u => 
            u.Email.ToLower() == normalizedEmail && u.NormalizedEmail != normalizedEmail);
        if (existingAdminUpper != null)
        {
            existingAdminUpper.NormalizedEmail = normalizedEmail;
            existingAdminUpper.Email = email.Trim();
            context.Users.Update(existingAdminUpper);
            await context.SaveChangesAsync();
        }

        var adminExists = await context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail);
        if (!adminExists)
        {
            var adminUser = new User
            {
                FullName = fullName,
                Email = email,
                NormalizedEmail = normalizedEmail,
                PasswordHash = passwordHasher.Hash(password),
                Status = UserStatus.Active,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };

            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();

            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Admin);
            if (adminRole != null)
            {
                var userRole = new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id,
                    AssignedAt = DateTime.UtcNow
                };

                await context.UserRoles.AddAsync(userRole);
                await context.SaveChangesAsync();
            }
        }
    }
}
