using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Dashboard;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Dashboard;

[Collection("Sequential")]
public sealed class AcademicOverviewIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AcademicOverview_ShouldStillLoadWhenNoSemesterIsActive()
    {
        var (token, semesterId) = await SeedLecturerWithClassAsync(2052);

        await WithNoActiveSemesterAsync(async () =>
        {
            var response = await GetOverviewAsync(token);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<AcademicOverviewResponse>>();

            response.StatusCode.Should().Be(HttpStatusCode.OK, "the dashboard must not fail just because no semester is active");
            body!.Data!.Scope.SemesterId.Should().Be(semesterId, "the lecturer's own semester is the best fallback");
            body.Data.Scope.IsActiveSemester.Should().BeFalse();
            body.Data.HasAssignedClasses.Should().BeTrue();
            body.Data.FilterOptions.Semesters.Should().Contain(item => item.Id == semesterId);
        });
    }

    [Fact]
    public async Task AcademicOverview_ShouldFallBackToTheLatestSemesterForALecturerWithoutClasses()
    {
        var (_, _) = await SeedLecturerWithClassAsync(2053);
        var emptyLecturerToken = await CreateLecturerTokenAsync();

        await WithNoActiveSemesterAsync(async () =>
        {
            var response = await GetOverviewAsync(emptyLecturerToken);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<AcademicOverviewResponse>>();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body!.Data!.Scope.IsActiveSemester.Should().BeFalse();
            body.Data.HasAssignedClasses.Should().BeFalse();
        });
    }

    [Fact]
    public async Task AcademicOverview_ShouldReportTheActiveSemesterWhenThereIsOne()
    {
        var (token, _) = await SeedLecturerWithClassAsync(2054);
        bool hasActive;
        using (var scope = factory.Services.CreateScope())
        {
            hasActive = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Semesters
                .AnyAsync(item => item.Status == SemesterStatus.Active);
        }

        var response = await GetOverviewAsync(token);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AcademicOverviewResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        if (hasActive) body!.Data!.Scope.IsActiveSemester.Should().BeTrue();
    }

    [Fact]
    public async Task AcademicOverview_ShouldRejectAnUnknownSemesterRequestedByTheClient()
    {
        var (token, _) = await SeedLecturerWithClassAsync(2055);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/dashboard/academic-overview?semesterId={Guid.NewGuid()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AcademicOverview_ShouldBeDeniedWithoutALecturer()
    {
        using var anonymous = await _client.GetAsync("/api/dashboard/academic-overview");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // The unique "one active semester" rule means the only way to reproduce "no active semester" is to move the
    // current one aside for the duration of the check and always put it back.
    private async Task WithNoActiveSemesterAsync(Func<Task> check)
    {
        Guid[] movedAside;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var active = await context.Semesters.Where(item => item.Status == SemesterStatus.Active).ToListAsync();
            movedAside = active.Select(item => item.Id).ToArray();
            foreach (var semester in active) semester.Status = SemesterStatus.Closing;
            await context.SaveChangesAsync();
        }

        try
        {
            await check();
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var restore = await context.Semesters.Where(item => movedAside.Contains(item.Id)).ToListAsync();
            foreach (var semester in restore) semester.Status = SemesterStatus.Active;
            await context.SaveChangesAsync();
        }
    }

    private async Task<HttpResponseMessage> GetOverviewAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/academic-overview");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<(string Token, Guid SemesterId)> SeedLecturerWithClassAsync(int year)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminId = await context.Users.Where(item => item.NormalizedEmail == "admin@ehub.test").Select(item => item.Id).SingleAsync();
        var lecturerRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Lecturer);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var email = $"overview-{suffix}@example.com";
        var lecturer = new User { FullName = $"Overview Lecturer {suffix}", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
        context.Users.Add(lecturer);
        context.UserRoles.Add(new UserRole { UserId = lecturer.Id, User = lecturer, RoleId = lecturerRole.Id, Role = lecturerRole, AssignedAt = DateTime.UtcNow, AssignedBy = adminId });

        var semester = new Semester { Code = $"OV{suffix}", Name = $"Overview {suffix}", Term = SemesterTerm.Spring, Year = year, Status = SemesterStatus.Planned };
        var course = new Course { Code = $"O{suffix}", Name = "Overview Course", Status = CourseStatus.Active };
        var @class = new Class
        {
            SemesterId = semester.Id, Semester = semester, CourseId = course.Id, Course = course, ClassCode = $"OC{suffix}", Slug = $"oc-{suffix}",
            ClassIndex = 1, Status = ClassStatus.Active, PrimaryLecturerId = lecturer.Id,
            ScheduleJson = "[{\"dayOfWeek\":1,\"startTime\":\"08:00\",\"endTime\":\"10:00\"}]", CreatedById = adminId
        };
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Classes.Add(@class);
        await context.SaveChangesAsync();

        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token;
        return (token, semester.Id);
    }

    private async Task<string> CreateLecturerTokenAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lecturerRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Lecturer);
        var email = $"overview-empty-{Guid.NewGuid():N}@example.com";
        var lecturer = new User { FullName = "Overview Lecturer Without Classes", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
        context.Users.Add(lecturer);
        context.UserRoles.Add(new UserRole { UserId = lecturer.Id, User = lecturer, RoleId = lecturerRole.Id, Role = lecturerRole, AssignedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        return scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token;
    }
}
