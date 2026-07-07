using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Infrastructure.Persistence.Seed;

public static class SemesterSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (!await context.Semesters.AnyAsync())
        {
            var semesters = new[]
            {
                new Semester
                {
                    Code = "SP2026",
                    Name = "Spring 2026",
                    Term = SemesterTerm.Spring,
                    Year = 2026,
                    StartDate = new DateOnly(2026, 1, 1),
                    EndDate = new DateOnly(2026, 4, 30),
                    Status = SemesterStatus.Active
                },
                new Semester
                {
                    Code = "SU2026",
                    Name = "Summer 2026",
                    Term = SemesterTerm.Summer,
                    Year = 2026,
                    StartDate = new DateOnly(2026, 5, 1),
                    EndDate = new DateOnly(2026, 8, 31),
                    Status = SemesterStatus.Planned
                },
                new Semester
                {
                    Code = "FA2026",
                    Name = "Fall 2026",
                    Term = SemesterTerm.Fall,
                    Year = 2026,
                    StartDate = new DateOnly(2026, 9, 1),
                    EndDate = new DateOnly(2026, 12, 31),
                    Status = SemesterStatus.Planned
                }
            };

            await context.Semesters.AddRangeAsync(semesters);
            await context.SaveChangesAsync();
        }
    }
}
