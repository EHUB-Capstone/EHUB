using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EHub.ApplicationTests.Features.Showcase;

internal sealed record SeededShowcase(
    Guid ShowcaseId, Guid LineageId, Guid OwnerUserId, Guid LecturerUserId, Guid ClassId);

internal static class ShowcaseTestData
{
    public static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static async Task<User> AddUserAsync(AppDbContext db, string name)
    {
        var user = new User { FullName = name, Email = $"{Guid.NewGuid():N}@example.test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Seeds a class (optionally with an assigned lecturer), an active team with one student, the team's project and a
    /// showcase for the project's lineage in the given status.
    /// </summary>
    public static async Task<SeededShowcase> SeedAsync(
        AppDbContext db,
        ShowcaseStatus status = ShowcaseStatus.Draft,
        bool withLecturer = true,
        bool complete = true,
        bool showTeamMembers = true,
        DateTime? publishedAt = null,
        Guid? lecturerUserId = null)
    {
        var lecturerId = lecturerUserId ?? (withLecturer ? (await AddUserAsync(db, "Lecturer")).Id : Guid.Empty);
        var ownerUserId = Guid.NewGuid();
        var @class = new Class
        {
            ClassCode = $"SE{Random.Shared.Next(1000, 9999)}",
            Slug = Guid.NewGuid().ToString("N"),
            PrimaryLecturerId = withLecturer ? lecturerId : null
        };
        var student = new Student { FullName = "Owner Student", UserId = ownerUserId, AvatarUrl = "https://cdn.example.test/a.png" };
        var enrollment = new ClassStudent
        {
            Class = @class, ClassId = @class.Id, Student = student, StudentId = student.Id, EnrollmentStatus = EnrollmentStatus.Active
        };
        var team = new Team { Class = @class, ClassId = @class.Id, TeamCode = Guid.NewGuid().ToString("N")[..8], TeamName = "Garden Team", Status = TeamStatus.Active };
        team.TeamMembers.Add(new TeamMember
        {
            Team = team, TeamId = team.Id, ClassId = @class.Id, StudentId = student.Id, ClassStudent = enrollment, CountsTowardActiveTeam = true
        });
        // Problem and solution are optional in the Project Profile; a page cannot be submitted without them.
        var project = new Project
        {
            Team = team, TeamId = team.Id, Name = "Smart Garden", ProjectLineageId = Guid.NewGuid(),
            Description = "Water plants automatically",
            Problem = complete ? "Plants die when owners travel" : null,
            Solution = complete ? "A sensor and a pump" : null,
            TargetUsers = "Busy students"
        };
        var showcase = new ShowcaseProject
        {
            ProjectLineageId = project.ProjectLineageId,
            Status = status,
            ShowTeamMembers = showTeamMembers,
            PublishedAt = publishedAt
        };
        db.Projects.Add(project);
        db.ShowcaseProjects.Add(showcase);
        await db.SaveChangesAsync();
        return new SeededShowcase(showcase.Id, project.ProjectLineageId, ownerUserId, lecturerId, @class.Id);
    }
}
