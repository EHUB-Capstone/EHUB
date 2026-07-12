using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;

namespace EHub.Infrastructure.Persistence.Seed;

public static class MentorSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (!await context.MentorProfiles.AnyAsync())
        {
            // Find the Mentor role
            var mentorRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == SystemRoles.Mentor);
            if (mentorRole != null)
            {
                // Create a sample mentor user if they don't exist
                var mentorEmail = "mentor.sample@ehub.edu.vn";
                var mentorUser = await context.Users.FirstOrDefaultAsync(u => u.Email == mentorEmail);

                if (mentorUser == null)
                {
                    mentorUser = new User
                    {
                        FullName = "Dr. John Doe (Sample Mentor)",
                        Email = mentorEmail,
                        PasswordHash = "AQAAAAIAAYagAAAAEG3J/d3f78u98h23gh82", // Placeholder hash
                        Status = UserStatus.Active,
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Users.AddAsync(mentorUser);
                    await context.SaveChangesAsync();

                    // Assign role
                    var userRole = new UserRole
                    {
                        UserId = mentorUser.Id,
                        RoleId = mentorRole.Id
                    };
                    await context.UserRoles.AddAsync(userRole);
                    await context.SaveChangesAsync();
                }

                // Create Mentor Profile
                var mentorProfile = new MentorProfile
                {
                    UserId = mentorUser.Id,
                    Expertise = new[] { "Business Model Canvas", "Pitching Skills", "Software Engineering" },
                    Bio = "Experienced software architect and serial entrepreneur with 10+ years of mentoring university startup projects.",
                    Organization = "E-HUB Mentor Network",
                    LinkedInUrl = "https://linkedin.com/in/sample-mentor",
                    Status = MentorProfileStatus.Active,
                    MaxTeams = 5,
                    CreatedAt = DateTime.UtcNow
                };

                await context.MentorProfiles.AddAsync(mentorProfile);
                await context.SaveChangesAsync();
            }
        }
    }
}
