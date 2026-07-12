using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Infrastructure.Persistence.Seed;

public static class RubricSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (!await context.Rubrics.AnyAsync())
        {
            // Try to find the Course EXE201
            var exe201 = await context.Courses.FirstOrDefaultAsync(c => c.Code == "EXE201");
            if (exe201 != null)
            {
                // Try to find Checkpoint 1 of EXE201
                var checkpoint1 = await context.Checkpoints
                    .FirstOrDefaultAsync(cp => cp.CourseId == exe201.Id && cp.CheckpointNumber == 1);

                if (checkpoint1 != null)
                {
                    var rubric = new Rubric
                    {
                        Name = "EXE201 Checkpoint 1 - Startup Idea Rubric",
                        Description = "Evaluation criteria for the Startup Idea Proposal submission.",
                        CourseId = exe201.Id,
                        CheckpointId = checkpoint1.Id,
                        TotalWeight = 100,
                        Status = RubricStatus.Active,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Rubrics.AddAsync(rubric);
                    await context.SaveChangesAsync();

                    var criteria = new[]
                    {
                        new RubricCriterion
                        {
                            RubricId = rubric.Id,
                            Name = "Problem Clarity",
                            Description = "Clearly defined problem statement, customer pain points, and target persona.",
                            MaxScore = 10,
                            Weight = 20,
                            DisplayOrder = 1,
                            CreatedAt = DateTime.UtcNow
                        },
                        new RubricCriterion
                        {
                            RubricId = rubric.Id,
                            Name = "Solution Fit",
                            Description = "Proposed solution directly addresses the problem and delivers clear value.",
                            MaxScore = 10,
                            Weight = 20,
                            DisplayOrder = 2,
                            CreatedAt = DateTime.UtcNow
                        },
                        new RubricCriterion
                        {
                            RubricId = rubric.Id,
                            Name = "Market Potential",
                            Description = "Identified market size, potential competitors, and competitive advantage.",
                            MaxScore = 10,
                            Weight = 20,
                            DisplayOrder = 3,
                            CreatedAt = DateTime.UtcNow
                        },
                        new RubricCriterion
                        {
                            RubricId = rubric.Id,
                            Name = "Business Model",
                            Description = "Revenue model, pricing strategy, and initial cost assumptions are logical.",
                            MaxScore = 10,
                            Weight = 20,
                            DisplayOrder = 4,
                            CreatedAt = DateTime.UtcNow
                        },
                        new RubricCriterion
                        {
                            RubricId = rubric.Id,
                            Name = "Presentation",
                            Description = "Pitch quality, slide organization, professional delivery, and Q&A answers.",
                            MaxScore = 10,
                            Weight = 20,
                            DisplayOrder = 5,
                            CreatedAt = DateTime.UtcNow
                        }
                    };

                    await context.RubricCriteria.AddRangeAsync(criteria);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
