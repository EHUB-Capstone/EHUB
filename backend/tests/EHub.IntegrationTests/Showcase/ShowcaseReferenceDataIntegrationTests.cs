using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Showcase;

[Collection("Sequential")]
public sealed class ShowcaseReferenceDataIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task ReferenceData_IsSentOnlyToAdminAndInScopeLecturer_WhileIndustriesArePublic()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var key = Guid.NewGuid().ToString("N");
        var secret = $"SECRET-{key}";
        User NewUser(string label) => new()
        {
            FullName = label, Email = $"ref-{label}-{key}@example.test", NormalizedEmail = $"ref-{label}-{key}@example.test".ToUpperInvariant()
        };

        var admin = NewUser("admin");
        var lecturer = NewUser("lecturer");
        var foreignLecturer = NewUser("foreign");
        var owner = NewUser("owner");
        var otherStudent = NewUser("student");
        var mentor = NewUser("mentor");
        db.Users.AddRange(admin, lecturer, foreignLecturer, owner, otherStudent, mentor);
        var courseId = await db.Courses.Select(course => course.Id).FirstAsync();
        var semester = await db.Semesters.FirstAsync();
        var classIndex = (await db.Classes.IgnoreQueryFilters()
            .Where(item => item.CourseId == courseId && item.SemesterId == semester.Id)
            .MaxAsync(item => (int?)item.ClassIndex) ?? 0) + 1;
        var @class = new Class
        {
            ClassCode = $"Ref-{key}", Slug = $"ref-{key}", ClassIndex = classIndex,
            CourseId = courseId, SemesterId = semester.Id, PrimaryLecturerId = lecturer.Id
        };
        @class.ClassLecturers.Add(new ClassLecturer { LecturerId = lecturer.Id, IsPrimary = true });
        var student = new Student { FullName = owner.FullName, User = owner };
        var enrollment = new ClassStudent { Class = @class, Student = student, CourseId = courseId, SemesterId = semester.Id };
        var team = new Team { TeamCode = key[..8], TeamName = "Reference team", Class = @class };
        team.TeamMembers.Add(new TeamMember { ClassStudent = enrollment });
        var project = new Project { Team = team, Name = $"Reference {key}" };
        var showcase = new ShowcaseProject
        {
            ProjectLineageId = project.ProjectLineageId, Status = ShowcaseStatus.Published, PublishedAt = DateTime.UtcNow
        };
        var rubric = new Rubric { Name = $"Rubric {key}" };
        db.Projects.Add(project);
        db.ShowcaseProjects.Add(showcase);
        db.Rubrics.Add(rubric);
        await db.SaveChangesAsync();

        var industry = $"RefIndustry-{key[..8]}";
        db.ProjectTags.AddRange(
            new ProjectTag { ProjectId = project.Id, TagName = industry, NormalizedTagName = industry.ToUpperInvariant(), TagType = ProjectTagType.StartupField },
            new ProjectTag { ProjectId = project.Id, TagName = "Keyword-only", NormalizedTagName = "KEYWORD-ONLY", TagType = ProjectTagType.Keyword });
        Evaluation Evaluate(EvaluatorRole role, EvaluationStatus status, decimal score, int daysAgo) => new()
        {
            ProjectId = project.Id, RubricId = rubric.Id, EvaluatorId = lecturer.Id, EvaluatorRole = role, Status = status,
            TotalScore = score, MaxTotalScore = 100, Strengths = $"{secret}-{role}-{score}",
            PublishedAt = DateTime.UtcNow.AddDays(-daysAgo)
        };
        db.Evaluations.AddRange(
            Evaluate(EvaluatorRole.Lecturer, EvaluationStatus.Published, 70, 5),
            Evaluate(EvaluatorRole.Lecturer, EvaluationStatus.Locked, 85, 1),
            Evaluate(EvaluatorRole.Lecturer, EvaluationStatus.Draft, 99, 0),
            Evaluate(EvaluatorRole.Mentor, EvaluationStatus.Published, 60, 2),
            Evaluate(EvaluatorRole.Admin, EvaluationStatus.Submitted, 55, 0));
        await db.SaveChangesAsync();
        using var client = factory.CreateClient();

        async Task<(string Json, ShowcaseDetailDto Detail)> GetAsync(User user, string role)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/showcase/{showcase.Id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GenerateAccessToken(user, [role]).Token);
            using var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await response.Content.ReadAsStringAsync();
            var detail = (await System.Text.Json.JsonSerializer.DeserializeAsync<ApiResponse<ShowcaseDetailDto>>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)),
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!.Data!;
            return (json, detail);
        }

        try
        {
            foreach (var (user, role) in new[] { (admin, SystemRoles.Admin), (lecturer, SystemRoles.Lecturer) })
            {
                var (_, detail) = await GetAsync(user, role);
                detail.Industries.Should().Equal(industry);
                var reference = detail.ReferenceData;
                reference.Should().NotBeNull();
                reference!.ClassCode.Should().Be(@class.ClassCode);
                reference.Semester!.Id.Should().Be(semester.Id);
                reference.Semester.Code.Should().Be(semester.Code);
                reference.Evaluations.Should().HaveCount(2, "only the newest Published/Locked evaluation of each role is shown");
                reference.Evaluations.Single(item => item.EvaluatorRole == "Lecturer").TotalScore.Should().Be(85);
                reference.Evaluations.Single(item => item.EvaluatorRole == "Mentor").TotalScore.Should().Be(60);
                reference.Evaluations.Should().NotContain(item => item.TotalScore == 99 || item.TotalScore == 55);
            }

            foreach (var (user, role) in new[]
                     {
                         (owner, SystemRoles.Student), (otherStudent, SystemRoles.Student),
                         (mentor, SystemRoles.Mentor), (foreignLecturer, SystemRoles.Lecturer)
                     })
            {
                var (json, detail) = await GetAsync(user, role);
                detail.Industries.Should().Equal([industry], $"{role} still sees the public industry tag");
                detail.ReferenceData.Should().BeNull($"{role} must not receive reference data");
                json.Should().NotContain(secret).And.NotContain("totalScore").And.NotContain("strengths")
                    .And.NotContain(@class.ClassCode);
            }
        }
        finally
        {
            db.ChangeTracker.Clear();
            var userIds = new[] { admin.Id, lecturer.Id, foreignLecturer.Id, owner.Id, otherStudent.Id, mentor.Id };
            await db.Evaluations.Where(item => item.ProjectId == project.Id).ExecuteDeleteAsync();
            await db.Rubrics.Where(item => item.Id == rubric.Id).ExecuteDeleteAsync();
            await db.ProjectTags.Where(item => item.ProjectId == project.Id).ExecuteDeleteAsync();
            await db.ShowcaseViewsDaily.Where(item => item.ShowcaseProjectId == showcase.Id).ExecuteDeleteAsync();
            await db.TeamMembers.Where(item => item.TeamId == team.Id).ExecuteDeleteAsync();
            await db.ClassStudents.Where(item => item.ClassId == @class.Id).ExecuteDeleteAsync();
            await db.ClassLecturers.Where(item => item.ClassId == @class.Id).ExecuteDeleteAsync();
            db.ShowcaseProjects.RemoveRange(await db.ShowcaseProjects.Where(item => item.Id == showcase.Id).ToListAsync());
            db.Projects.RemoveRange(await db.Projects.Where(item => item.Id == project.Id).ToListAsync());
            db.Teams.RemoveRange(await db.Teams.Where(item => item.Id == team.Id).ToListAsync());
            db.Classes.Remove(await db.Classes.SingleAsync(item => item.Id == @class.Id));
            db.Students.Remove(await db.Students.SingleAsync(item => item.Id == student.Id));
            db.Users.RemoveRange(await db.Users.Where(item => userIds.Contains(item.Id)).ToListAsync());
            await db.SaveChangesAsync();
        }
    }
}
