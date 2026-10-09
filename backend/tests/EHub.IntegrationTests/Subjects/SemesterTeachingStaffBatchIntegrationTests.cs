using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Subjects;

[Collection("Sequential")]
public sealed class SemesterTeachingStaffBatchIntegrationTests(CustomWebApplicationFactory factory)
{
    private const string BatchUrl = "/api/subjects/teaching-staff/batch";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AddBatch_ShouldAddEveryEligibleMentorAndReportTheOthers()
    {
        var token = await GetAdminTokenAsync();
        var data = await SeedAsync(2080, SemesterStatus.Planned);
        var unknownId = Guid.NewGuid();

        var response = await PostBatchAsync(token, data, "MENTOR",
            [.. data.NewMentors, data.ActiveEntryMentor, data.InactiveEntryMentor, data.Lecturer, data.InactiveUser, unknownId]);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AddSemesterTeachingStaffBatchResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = body!.Data!;
        result.AddedCount.Should().Be(3);
        result.AlreadyInListCount.Should().Be(2);
        result.RejectedCount.Should().Be(3);
        result.Results.Where(item => item.Outcome == SemesterStaffBatchOutcomes.Added).Select(item => item.UserId)
            .Should().BeEquivalentTo(data.NewMentors);
        result.Results.Single(item => item.UserId == data.InactiveEntryMentor).Message.Should().Contain("reactivate");
        result.Results.Single(item => item.UserId == data.Lecturer).Outcome.Should().Be(SemesterStaffBatchOutcomes.Rejected);
        result.Results.Single(item => item.UserId == data.InactiveUser).Outcome.Should().Be(SemesterStaffBatchOutcomes.Rejected);
        result.Results.Single(item => item.UserId == unknownId).Outcome.Should().Be(SemesterStaffBatchOutcomes.Rejected);
        result.Results.Where(item => item.Outcome == SemesterStaffBatchOutcomes.Added)
            .Should().OnlyContain(item => item.Staff != null && item.Staff.Status == "Active" && item.Staff.Role == "MENTOR");

        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().SemesterStaffAssignments.AsNoTracking()
            .Where(item => item.SemesterId == data.SemesterId).ToListAsync();
        saved.Should().HaveCount(5, "3 new entries plus the 2 that existed before");
        saved.Where(item => data.NewMentors.Contains(item.UserId)).Should().OnlyContain(item => item.Status == SemesterStaffStatus.Active);
        saved.Single(item => item.UserId == data.InactiveEntryMentor).Status.Should().Be(SemesterStaffStatus.Inactive, "an inactive entry is never reactivated by adding");
    }

    [Fact]
    public async Task AddBatch_ShouldBeRepeatableAndProcessDuplicateIdsOnce()
    {
        var token = await GetAdminTokenAsync();
        var data = await SeedAsync(2081, SemesterStatus.Planned);
        var ids = new[] { data.NewMentors[0], data.NewMentors[0], data.NewMentors[1] };

        var first = await (await PostBatchAsync(token, data, "MENTOR", ids)).Content.ReadFromJsonAsync<ApiResponse<AddSemesterTeachingStaffBatchResponse>>();
        var second = await (await PostBatchAsync(token, data, "MENTOR", ids)).Content.ReadFromJsonAsync<ApiResponse<AddSemesterTeachingStaffBatchResponse>>();

        first!.Data!.AddedCount.Should().Be(2, "the repeated id is handled once");
        second!.Data!.AddedCount.Should().Be(0);
        second.Data.AlreadyInListCount.Should().Be(2);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().SemesterStaffAssignments.AsNoTracking()
            .CountAsync(item => item.SemesterId == data.SemesterId && data.NewMentors.Contains(item.UserId))).Should().Be(2);
    }

    [Fact]
    public async Task AddBatch_ShouldAddLecturersToo()
    {
        var token = await GetAdminTokenAsync();
        var data = await SeedAsync(2082, SemesterStatus.Planned);

        var response = await PostBatchAsync(token, data, "LECTURER", [data.Lecturer, data.NewMentors[0]]);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AddSemesterTeachingStaffBatchResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Data!.AddedCount.Should().Be(1);
        body.Data.RejectedCount.Should().Be(1, "a mentor does not have the Lecturer role");
        body.Data.Results.Single(item => item.UserId == data.Lecturer).Staff!.Role.Should().Be("LECTURER");
    }

    [Theory]
    [InlineData("Closing")]
    [InlineData("Archived")]
    public async Task AddBatch_ShouldNotChangeTheStaffOfASemesterThatIsNoLongerOpen(string status)
    {
        var token = await GetAdminTokenAsync();
        var data = await SeedAsync(status == "Closing" ? 2083 : 2084, Enum.Parse<SemesterStatus>(status));

        var response = await PostBatchAsync(token, data, "MENTOR", data.NewMentors);

        response.IsSuccessStatusCode.Should().BeFalse();
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().SemesterStaffAssignments.AsNoTracking()
            .CountAsync(item => item.SemesterId == data.SemesterId && data.NewMentors.Contains(item.UserId))).Should().Be(0);
    }

    [Fact]
    public async Task AddBatch_ShouldRejectInvalidRequests()
    {
        var token = await GetAdminTokenAsync();
        var data = await SeedAsync(2085, SemesterStatus.Planned);

        (await PostBatchAsync(token, data, "MENTOR", [])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PostBatchAsync(token, data, "MENTOR", Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray())).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await PostBatchAsync(token, data, "ADMIN", data.NewMentors)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PostBatchAsync(token, data with { SemesterCode = "XX" }, "MENTOR", data.NewMentors)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PostBatchAsync(token, data with { Year = 2040 }, "MENTOR", data.NewMentors)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddBatch_ShouldBeDeniedWithoutAnAdministrator()
    {
        var data = await SeedAsync(2086, SemesterStatus.Planned);
        var payload = new AddSemesterTeachingStaffBatchRequest { Semester = data.SemesterCode, Year = data.Year, Role = "MENTOR", UserIds = data.NewMentors };

        using var anonymous = await _client.PostAsJsonAsync(BatchUrl, payload);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        string lecturerToken;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lecturerRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Lecturer);
            var user = await context.Users.SingleAsync(item => item.Id == data.Lecturer);
            lecturerToken = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
                .GenerateAccessToken(user, [lecturerRole.Name]).Token;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BatchUrl) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lecturerToken);
        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Candidates_ShouldTellIndustryMentorsFromLecturerMentors()
    {
        var token = await GetAdminTokenAsync();
        var data = await SeedAsync(2088, SemesterStatus.Planned);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/subjects/teaching-staff/candidates");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<TeachingStaffCandidateListResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var industry = body!.Data!.Candidates.Single(item => item.UserId == data.NewMentors[0] && item.Role == "MENTOR");
        industry.MentorType.Should().Be("Enterprise");
        industry.ContractType.Should().Be("Thỉnh giảng");
        body.Data.Candidates.Single(item => item.UserId == data.NewMentors[1] && item.Role == "MENTOR").MentorType.Should().Be("Academic");
        body.Data.Candidates.Single(item => item.UserId == data.Lecturer && item.Role == "LECTURER").MentorType.Should().BeNull();
    }

    private async Task<HttpResponseMessage> PostBatchAsync(string token, BatchSeed data, string role, IReadOnlyCollection<Guid> userIds)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BatchUrl)
        {
            Content = JsonContent.Create(new AddSemesterTeachingStaffBatchRequest
            {
                Semester = data.SemesterCode, Year = data.Year, Role = role, UserIds = userIds
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = "admin@ehub.test", Password = "Admin@123456" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    private sealed record BatchSeed(
        Guid SemesterId, string SemesterCode, int Year, Guid[] NewMentors, Guid ActiveEntryMentor, Guid InactiveEntryMentor,
        Guid Lecturer, Guid InactiveUser);

    // A Spring semester of the given year with: 3 mentors not yet in it (first industry, second lecturer mentor), one mentor
    // already active in it, one mentor listed as inactive, a lecturer, and a mentor whose account is inactive.
    private async Task<BatchSeed> SeedAsync(int year, SemesterStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminId = await context.Users.Where(item => item.NormalizedEmail == "admin@ehub.test").Select(item => item.Id).SingleAsync();
        var mentorRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Mentor);
        var lecturerRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Lecturer);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var semester = new Semester { Code = $"SB{suffix}"[..10], Name = $"Batch {suffix}", Term = SemesterTerm.Spring, Year = year, Status = status };
        context.Semesters.Add(semester);

        User NewUser(string label, Role role, UserStatus userStatus = UserStatus.Active)
        {
            var email = $"batch-{label}-{suffix}@example.com";
            var user = new User { FullName = $"Batch {label}", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = userStatus };
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = role.Id, Role = role, AssignedAt = DateTime.UtcNow, AssignedBy = adminId });
            return user;
        }

        void AddProfile(User user, MentorType type, string? contract) =>
            context.MentorProfiles.Add(new MentorProfile { UserId = user.Id, User = user, Type = type, ContractType = contract, Status = MentorProfileStatus.Active, CreatedBy = adminId });

        void EnrolIn(User user, SemesterStaffStatus entryStatus) =>
            context.SemesterStaffAssignments.Add(new SemesterStaffAssignment
            {
                SemesterId = semester.Id, Semester = semester, UserId = user.Id, User = user, Role = SemesterStaffRole.Mentor, Status = entryStatus, CreatedBy = adminId
            });

        var industry = NewUser("industry", mentorRole);
        var academic = NewUser("academic", mentorRole);
        var third = NewUser("third", mentorRole);
        var active = NewUser("active", mentorRole);
        var inactiveEntry = NewUser("inactive-entry", mentorRole);
        var lecturer = NewUser("lecturer", lecturerRole);
        var inactiveUser = NewUser("inactive-user", mentorRole, UserStatus.Inactive);
        AddProfile(industry, MentorType.Enterprise, "Thỉnh giảng");
        AddProfile(academic, MentorType.Academic, null);
        AddProfile(third, MentorType.Enterprise, "Khoán");
        AddProfile(active, MentorType.Enterprise, "Khoán");
        AddProfile(inactiveEntry, MentorType.Enterprise, "Khoán");
        EnrolIn(active, SemesterStaffStatus.Active);
        EnrolIn(inactiveEntry, SemesterStaffStatus.Inactive);

        await context.SaveChangesAsync();
        return new BatchSeed(semester.Id, "SP", year, [industry.Id, academic.Id, third.Id], active.Id, inactiveEntry.Id, lecturer.Id, inactiveUser.Id);
    }
}
