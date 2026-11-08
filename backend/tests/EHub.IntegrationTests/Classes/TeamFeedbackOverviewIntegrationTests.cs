using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EHub.IntegrationTests.Classes;

[Collection("Sequential")]
public sealed class TeamFeedbackOverviewIntegrationTests
{
    private static int _yearCounter = 4000;

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TeamFeedbackOverviewIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FeedbackOverview_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync($"/api/teams/{Guid.NewGuid()}/feedback-overview");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FeedbackOverview_ChecksRoleAndResourceAccess()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (teamId, lecturer, otherLecturer, admin) = await SeedAsync(context);

        var allowed = await _client.SendAsync(Authorized(teamId, GenerateToken(scope.ServiceProvider, lecturer, SystemRoles.Lecturer)));
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await allowed.Content.ReadFromJsonAsync<ApiResponse<TeamFeedbackOverviewDto>>();
        body!.Data.TeamId.Should().Be(teamId);
        body.Data.Checkpoints.Should().ContainSingle().Which.Title.Should().Be("Checkpoint 1");
        allowed.Headers.CacheControl!.NoStore.Should().BeTrue();

        // A lecturer of another class cannot read the team by guessing its id (IDOR).
        var forbidden = await _client.SendAsync(Authorized(teamId, GenerateToken(scope.ServiceProvider, otherLecturer, SystemRoles.Lecturer)));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await forbidden.Content.ReadFromJsonAsync<ApiResponse<object>>();
        error!.Code.Should().Be(ErrorCodes.ClassAccessDenied);

        var adminOk = await _client.SendAsync(Authorized(teamId, GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin)));
        adminOk.StatusCode.Should().Be(HttpStatusCode.OK);

        var missing = await _client.SendAsync(Authorized(Guid.NewGuid(), GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin)));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<(Guid TeamId, User Lecturer, User OtherLecturer, User Admin)> SeedAsync(AppDbContext context)
    {
        var admin = await context.Users
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .FirstAsync(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin));
        var lecturer = await CreateUserAsync(context, SystemRoles.Lecturer, "feedback-lecturer");
        var otherLecturer = await CreateUserAsync(context, SystemRoles.Lecturer, "feedback-other");

        var year = Interlocked.Increment(ref _yearCounter);
        var unique = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var semester = new Semester
        {
            Code = $"F{year}",
            Name = $"Feedback {year}",
            Term = SemesterTerm.Fall,
            Year = year,
            StartDate = new DateOnly(year, 9, 1),
            EndDate = new DateOnly(year, 12, 20),
            Status = SemesterStatus.Planned,
            CreatedBy = admin.Id
        };
        var course = new Course { Code = $"F{unique}", Name = $"Feedback course {unique}", Status = CourseStatus.Active, CreatedBy = admin.Id };
        var @class = new Class
        {
            ClassCode = $"{course.Code}_1",
            Slug = $"{semester.Code}-{course.Code}-1".ToLowerInvariant(),
            ClassIndex = 1,
            CourseId = course.Id,
            SemesterId = semester.Id,
            PrimaryLecturerId = lecturer.Id,
            ScheduleJson = "[{\"dayOfWeek\":1,\"slotNumber\":1,\"room\":\"C-101\"}]",
            Status = ClassStatus.Active,
            CreatedById = admin.Id,
            CreatedBy = admin.Id
        };
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Classes.Add(@class);
        context.ClassLecturers.Add(new ClassLecturer { ClassId = @class.Id, LecturerId = lecturer.Id, IsPrimary = true, AssignedById = admin.Id });
        await context.SaveChangesAsync();

        var team = new Team
        {
            ClassId = @class.Id,
            TeamCode = $"{@class.ClassCode}_TEAM_1",
            TeamName = $"Feedback team {unique}",
            Status = TeamStatus.Active,
            CreatedById = admin.Id
        };
        context.Teams.Add(team);
        context.Checkpoints.Add(new Checkpoint
        {
            CourseId = course.Id,
            Name = "Checkpoint 1",
            CheckpointNumber = 1,
            CourseWeight = 10,
            Status = CheckpointStatus.Open,
            CreatedById = admin.Id
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return (team.Id, lecturer, otherLecturer, admin);
    }

    private static async Task<User> CreateUserAsync(AppDbContext context, string roleName, string suffix)
    {
        var role = await context.Roles.SingleAsync(item => item.Name == roleName);
        var email = $"{suffix}-{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            FullName = $"Feedback {suffix}",
            Email = email,
            NormalizedEmail = email.ToLowerInvariant(),
            PasswordHash = "integration-test-only",
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role });
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return user;
    }

    private static string GenerateToken(IServiceProvider services, User user, string role) =>
        services.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [role]).Token;

    private static HttpRequestMessage Authorized(Guid teamId, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/teams/{teamId}/feedback-overview");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
