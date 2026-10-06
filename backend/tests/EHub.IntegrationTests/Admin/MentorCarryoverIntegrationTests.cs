using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.IntegrationTests.Common;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Admin;

[Collection("Sequential")]
public sealed class MentorCarryoverIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Preview_ShouldReturn401_WhenNoTokenIsProvided()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/subjects/teaching-staff/mentor-carryover/preview",
            new PreviewMentorCarryoverRequest
            {
                SourceSemesterId = Guid.NewGuid(),
                TargetSemesterId = Guid.NewGuid()
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Preview_ShouldReturn403_WhenLecturerTokenIsProvided()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lecturerRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Lecturer);
        var suffix = Guid.NewGuid().ToString("N");
        var lecturer = new User
        {
            FullName = "Carryover Forbidden Lecturer",
            Email = $"carryover-forbidden-{suffix}@example.com",
            NormalizedEmail = $"carryover-forbidden-{suffix}@example.com",
            PasswordHash = "not-used",
            Status = UserStatus.Active
        };
        context.Users.Add(lecturer);
        context.UserRoles.Add(new UserRole
        {
            UserId = lecturer.Id,
            User = lecturer,
            RoleId = lecturerRole.Id,
            Role = lecturerRole,
            AssignedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token;

        using var request = AuthorizedPost(
            "/api/subjects/teaching-staff/mentor-carryover/preview",
            new PreviewMentorCarryoverRequest
            {
                SourceSemesterId = Guid.NewGuid(),
                TargetSemesterId = Guid.NewGuid()
            },
            token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_ShouldPreviewAndCarrySelectedMentorsWithoutCopyingOldAssignments()
    {
        var token = await GetAdminTokenAsync();
        Guid sourceSemesterId;
        Guid targetSemesterId;
        Guid addUserId;
        Guid reactivateUserId;
        Guid alreadyAddedUserId;
        Guid unavailableUserId;

        using (var setupScope = factory.Services.CreateScope())
        {
            var context = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminId = await context.Users
                .Where(item => item.NormalizedEmail == "admin@ehub.test")
                .Select(item => item.Id)
                .SingleAsync();
            var mentorRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Mentor);
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var source = new Semester
            {
                Code = $"CO-S-{suffix}",
                Name = $"Carryover source {suffix}",
                Term = SemesterTerm.Summer,
                Year = 2087,
                Status = SemesterStatus.Planned
            };
            var target = new Semester
            {
                Code = $"CO-T-{suffix}",
                Name = $"Carryover target {suffix}",
                Term = SemesterTerm.Fall,
                Year = 2087,
                Status = SemesterStatus.Planned
            };
            context.Semesters.AddRange(source, target);

            var addMentor = CreateMentor("Add", suffix, MentorType.Enterprise, mentorRole, adminId);
            var reactivateMentor = CreateMentor("Reactivate", suffix, MentorType.Academic, mentorRole, adminId);
            var alreadyAddedMentor = CreateMentor("Existing", suffix, MentorType.Enterprise, mentorRole, adminId);
            var unavailableMentor = CreateMentor("Unavailable", suffix, MentorType.Academic, mentorRole, adminId);
            unavailableMentor.Status = UserStatus.Blocked;
            context.Users.AddRange(addMentor, reactivateMentor, alreadyAddedMentor, unavailableMentor);
            context.SemesterStaffAssignments.AddRange(
                SourceAssignment(source, addMentor, adminId),
                SourceAssignment(source, reactivateMentor, adminId),
                SourceAssignment(source, alreadyAddedMentor, adminId),
                SourceAssignment(source, unavailableMentor, adminId),
                TargetAssignment(target, reactivateMentor, SemesterStaffStatus.Inactive, adminId),
                TargetAssignment(target, alreadyAddedMentor, SemesterStaffStatus.Active, adminId));
            await context.SaveChangesAsync();

            sourceSemesterId = source.Id;
            targetSemesterId = target.Id;
            addUserId = addMentor.Id;
            reactivateUserId = reactivateMentor.Id;
            alreadyAddedUserId = alreadyAddedMentor.Id;
            unavailableUserId = unavailableMentor.Id;
        }

        using var previewRequest = AuthorizedPost(
            "/api/subjects/teaching-staff/mentor-carryover/preview",
            new PreviewMentorCarryoverRequest
            {
                SourceSemesterId = sourceSemesterId,
                TargetSemesterId = targetSemesterId
            },
            token);
        var previewResponse = await _client.SendAsync(previewRequest);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorCarryoverPreviewResponse>>();

        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        preview!.Data!.TotalCount.Should().Be(4);
        preview.Data.EligibleCount.Should().Be(2);
        preview.Data.AlreadyAddedCount.Should().Be(1);
        preview.Data.UnavailableCount.Should().Be(1);
        preview.Data.Mentors.Single(item => item.UserId == addUserId).Action.Should().Be("Add");
        preview.Data.Mentors.Single(item => item.UserId == reactivateUserId).Action.Should().Be("Reactivate");
        preview.Data.Mentors.Single(item => item.UserId == alreadyAddedUserId).CanSelect.Should().BeFalse();
        preview.Data.Mentors.Single(item => item.UserId == unavailableUserId).CanSelect.Should().BeFalse();

        using var invalidCommitRequest = AuthorizedPost(
            "/api/subjects/teaching-staff/mentor-carryover/commit",
            new CommitMentorCarryoverRequest
            {
                SourceSemesterId = sourceSemesterId,
                TargetSemesterId = targetSemesterId,
                MentorUserIds = [unavailableUserId]
            },
            token);
        var invalidCommitResponse = await _client.SendAsync(invalidCommitRequest);
        invalidCommitResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var commitRequest = AuthorizedPost(
            "/api/subjects/teaching-staff/mentor-carryover/commit",
            new CommitMentorCarryoverRequest
            {
                SourceSemesterId = sourceSemesterId,
                TargetSemesterId = targetSemesterId,
                MentorUserIds = [addUserId, reactivateUserId]
            },
            token);
        var commitResponse = await _client.SendAsync(commitRequest);
        var commit = await commitResponse.Content.ReadFromJsonAsync<ApiResponse<MentorCarryoverCommitResponse>>();

        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        commit!.Data!.AddedCount.Should().Be(1);
        commit.Data.ReactivatedCount.Should().Be(1);
        commit.Data.AlreadyAddedCount.Should().Be(0);

        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var copiedIds = new[] { addUserId, reactivateUserId, alreadyAddedUserId };
        var targetAssignments = await verifyContext.SemesterStaffAssignments.AsNoTracking()
            .Where(item => item.SemesterId == targetSemesterId && copiedIds.Contains(item.UserId))
            .ToListAsync();
        targetAssignments.Should().HaveCount(3);
        targetAssignments.Should().OnlyContain(item => item.Status == SemesterStaffStatus.Active);
        (await verifyContext.SemesterStaffAssignments.AsNoTracking()
            .AnyAsync(item => item.SemesterId == targetSemesterId && item.UserId == unavailableUserId))
            .Should().BeFalse();
        (await verifyContext.MentorAssignments.AsNoTracking()
            .CountAsync(item => copiedIds.Contains(item.MentorProfile.UserId) && item.Team.Class.SemesterId == targetSemesterId))
            .Should().Be(0);
        (await verifyContext.SemesterAuditLogs.AsNoTracking()
            .CountAsync(item => item.SemesterId == targetSemesterId && item.Action == "SEMESTER_MENTORS_CARRIED_OVER"))
            .Should().Be(1);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new EmailPasswordLoginRequest { Email = "admin@ehub.test", Password = "Admin@123456" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    private static User CreateMentor(
        string action,
        string suffix,
        MentorType type,
        Role mentorRole,
        Guid adminId)
    {
        var email = $"carryover-{action.ToLowerInvariant()}-{suffix}@example.com";
        var user = new User
        {
            FullName = $"Carryover {action} Mentor {suffix}",
            Email = email,
            NormalizedEmail = email,
            PasswordHash = "not-used",
            Status = UserStatus.Active
        };
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            User = user,
            RoleId = mentorRole.Id,
            Role = mentorRole,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = adminId
        });
        user.MentorProfile = new MentorProfile
        {
            UserId = user.Id,
            User = user,
            Type = type,
            Status = MentorProfileStatus.Active,
            CreatedBy = adminId
        };
        return user;
    }

    private static SemesterStaffAssignment SourceAssignment(Semester semester, User user, Guid adminId) =>
        TargetAssignment(semester, user, SemesterStaffStatus.Active, adminId);

    private static SemesterStaffAssignment TargetAssignment(
        Semester semester,
        User user,
        SemesterStaffStatus status,
        Guid adminId) => new()
        {
            SemesterId = semester.Id,
            Semester = semester,
            UserId = user.Id,
            User = user,
            Role = SemesterStaffRole.Mentor,
            Status = status,
            CreatedBy = adminId
        };

    private static HttpRequestMessage AuthorizedPost<T>(string url, T body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
