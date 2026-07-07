using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Infrastructure.Persistence.Seed;

public static class CourseSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (!await context.Courses.AnyAsync())
        {
            var courses = new[]
            {
                new Course
                {
                    Code = "EXE101",
                    Name = "Entrepreneurship 101",
                    Description = "Introduction to Entrepreneurship concepts, ideation and fundamental business models.",
                    Status = CourseStatus.Active
                },
                new Course
                {
                    Code = "EXE201",
                    Name = "Entrepreneurship 201",
                    Description = "Intermediate Entrepreneurship covering business validation, prototyping, and customer discovery.",
                    Status = CourseStatus.Active
                },
                new Course
                {
                    Code = "EXE401",
                    Name = "Entrepreneurship 401",
                    Description = "Advanced Entrepreneurship focusing on scaling, pitching to investors, and incubation preparation.",
                    Status = CourseStatus.Active
                }
            };

            await context.Courses.AddRangeAsync(courses);
            await context.SaveChangesAsync();
        }
    }
}
