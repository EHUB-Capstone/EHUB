using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Infrastructure.Persistence.Seed;

public static class CheckpointSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (!await context.Checkpoints.AnyAsync())
        {
            var courses = await context.Courses.ToListAsync();
            foreach (var course in courses)
            {
                var checkpoints = new[]
                {
                    new Checkpoint
                    {
                        CourseId = course.Id,
                        Name = "Checkpoint 1 - Startup Idea Proposal",
                        CheckpointNumber = 1,
                        Description = "Submit your initial startup concept, problem description, and target customer analysis.",
                        Status = CheckpointStatus.Open,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Checkpoint
                    {
                        CourseId = course.Id,
                        Name = "Checkpoint 2 - Business Model Canvas",
                        CheckpointNumber = 2,
                        Description = "Submit your Business Model Canvas, value proposition, and competitor analysis.",
                        Status = CheckpointStatus.Draft,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Checkpoint
                    {
                        CourseId = course.Id,
                        Name = "Checkpoint 3 - MVP / Prototype Demonstration",
                        CheckpointNumber = 3,
                        Description = "Submit details and link to your Minimum Viable Product or prototype demonstration.",
                        Status = CheckpointStatus.Draft,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Checkpoint
                    {
                        CourseId = course.Id,
                        Name = "Checkpoint 4 - Final Pitch Deck",
                        CheckpointNumber = 4,
                        Description = "Submit your final pitch deck presentation slides and financial plan overview.",
                        Status = CheckpointStatus.Draft,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await context.Checkpoints.AddRangeAsync(checkpoints);
            }

            await context.SaveChangesAsync();
        }
    }
}
