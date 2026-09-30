using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Subjects;

[Collection("Sequential")]
public sealed class SemesterTransitionIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public SemesterTransitionIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TransitionSemester_RequiresAuthenticationAndAdminRole()
    {
        using var client = _factory.CreateClient();
        var payload = InvalidPayload();

        using var anonymousResponse = await client.PostAsJsonAsync(
            "/api/subjects/current-semester/transition",
            payload);
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var lecturerRequest = AuthorizedRequest(
            HttpMethod.Post,
            "/api/subjects/current-semester/transition",
            SystemRoles.Lecturer,
            payload);
        using var lecturerResponse = await client.SendAsync(lecturerRequest);
        lecturerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TransitionSemester_WithInvalidPayload_Returns400ValidationError()
    {
        using var client = _factory.CreateClient();
        using var request = AuthorizedRequest(
            HttpMethod.Post,
            "/api/subjects/current-semester/transition",
            SystemRoles.Admin,
            InvalidPayload());

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotBeNull();
        body!.Code.Should().Be(ErrorCodes.CommonValidationError);
    }

    [Fact]
    public async Task TransitionSemester_MovesExpiredActiveToClosingAndTargetToActive()
    {
        var startedAtUtc = DateTime.UtcNow.AddSeconds(-1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid currentSemesterId;
        Guid targetSemesterId;
        DateOnly? originalCurrentStartDate;
        DateOnly? originalCurrentEndDate;
        DateOnly? originalTargetStartDate;
        DateOnly? originalTargetEndDate;
        TransitionSemesterRequest payload;
        string token;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await context.Semesters.SingleAsync(item => item.Status == SemesterStatus.Active);
            var target = await context.Semesters
                .Where(item => item.Status == SemesterStatus.Planned &&
                    (item.Year > current.Year || item.Year == current.Year && item.Term > current.Term))
                .OrderBy(item => item.Year)
                .ThenBy(item => item.Term)
                .FirstAsync();
            var admin = await context.Users
                .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .FirstAsync(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin));

            currentSemesterId = current.Id;
            targetSemesterId = target.Id;
            originalCurrentStartDate = current.StartDate;
            originalCurrentEndDate = current.EndDate;
            originalTargetStartDate = target.StartDate;
            originalTargetEndDate = target.EndDate;

            current.StartDate = today.AddDays(-60);
            current.EndDate = today.AddDays(-1);
            target.StartDate = today;
            target.EndDate = today.AddDays(60);
            await context.SaveChangesAsync();

            payload = new TransitionSemesterRequest
            {
                CurrentSemesterId = current.Id,
                CurrentRowVersion = current.Version.ToString(),
                TargetSemesterId = target.Id,
                TargetRowVersion = target.Version.ToString(),
                Reason = "Start the next semester while prior classes are being closed"
            };
            token = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);
        }

        try
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/subjects/current-semester/transition")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<CurrentSemesterResponse>>();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotBeNull();
            body!.Data!.CurrentSemester!.Id.Should().Be(targetSemesterId);

            await using var verificationScope = _factory.Services.CreateAsyncScope();
            var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var currentStatus = await verificationContext.Semesters
                .Where(item => item.Id == currentSemesterId)
                .Select(item => item.Status)
                .SingleAsync();
            var targetStatus = await verificationContext.Semesters
                .Where(item => item.Id == targetSemesterId)
                .Select(item => item.Status)
                .SingleAsync();

            currentStatus.Should().Be(SemesterStatus.Closing);
            targetStatus.Should().Be(SemesterStatus.Active);
            (await verificationContext.SemesterAuditLogs.CountAsync(item =>
                item.OccurredAtUtc >= startedAtUtc &&
                (item.SemesterId == currentSemesterId || item.SemesterId == targetSemesterId))).Should().Be(2);
        }
        finally
        {
            await using var cleanupScope = _factory.Services.CreateAsyncScope();
            var cleanupContext = cleanupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var current = await cleanupContext.Semesters.SingleAsync(item => item.Id == currentSemesterId);
            var target = await cleanupContext.Semesters.SingleAsync(item => item.Id == targetSemesterId);

            if (target.Status == SemesterStatus.Active)
            {
                target.Status = SemesterStatus.Planned;
                await cleanupContext.SaveChangesAsync();
            }

            current.Status = SemesterStatus.Active;
            current.StartDate = originalCurrentStartDate;
            current.EndDate = originalCurrentEndDate;
            target.StartDate = originalTargetStartDate;
            target.EndDate = originalTargetEndDate;

            var auditLogs = await cleanupContext.SemesterAuditLogs
                .Where(item => item.OccurredAtUtc >= startedAtUtc &&
                    (item.SemesterId == currentSemesterId || item.SemesterId == targetSemesterId))
                .ToListAsync();
            var outboxMessages = await cleanupContext.OutboxMessages
                .Where(item => item.OccurredAtUtc >= startedAtUtc &&
                    (item.AggregateId == currentSemesterId || item.AggregateId == targetSemesterId) &&
                    (item.Type == "Semester.TransitionedToClosing.v1" ||
                     item.Type == "Semester.ActivatedByTransition.v1"))
                .ToListAsync();
            cleanupContext.SemesterAuditLogs.RemoveRange(auditLogs);
            cleanupContext.OutboxMessages.RemoveRange(outboxMessages);
            await cleanupContext.SaveChangesAsync();
        }
    }

    private HttpRequestMessage AuthorizedRequest(
        HttpMethod method,
        string path,
        string role,
        object payload)
    {
        using var scope = _factory.Services.CreateScope();
        var user = new User
        {
            FullName = $"{role} semester transition tester",
            Email = $"semester-transition-{role.ToLowerInvariant()}@example.test"
        };
        var token = GenerateToken(scope.ServiceProvider, user, role);
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static string GenerateToken(IServiceProvider services, User user, string role) =>
        services.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [role]).Token;

    private static TransitionSemesterRequest InvalidPayload() => new()
    {
        CurrentSemesterId = Guid.Empty,
        CurrentRowVersion = "invalid",
        TargetSemesterId = Guid.Empty,
        TargetRowVersion = "invalid",
        Reason = "x"
    };
}
