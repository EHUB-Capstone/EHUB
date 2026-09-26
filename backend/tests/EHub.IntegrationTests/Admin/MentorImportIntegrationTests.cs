using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
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
public sealed class MentorImportIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DatabaseSeeder_ShouldNotCreateSampleMentor()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        var sampleExists = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.Email.ToLower() == "mentor.sample@ehub.edu.vn" &&
                              item.FullName == "Dr. John Doe (Sample Mentor)");

        appliedMigrations.Should().Contain("20260926090000_RemoveSampleMentorSeed");
        sampleExists.Should().BeFalse();
    }

    [Fact]
    public async Task Preview_ShouldReturn401_WhenNoTokenIsProvided()
    {
        var semesterId = await GetSemesterIdAsync();
        using var request = CreatePreviewRequest(semesterId, CreateMentorWorkbook("unauthorized-enterprise@example.com", "unauthorized-academic@example.com"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Preview_ShouldReturn403_WhenLecturerTokenIsProvided()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lecturerRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Lecturer);
        var lecturer = new User
        {
            FullName = "Forbidden Lecturer",
            Email = $"forbidden-{Guid.NewGuid():N}@example.com",
            NormalizedEmail = $"forbidden-{Guid.NewGuid():N}@example.com",
            PasswordHash = "not-used",
            Status = UserStatus.Active
        };
        lecturer.NormalizedEmail = lecturer.Email;
        context.Users.Add(lecturer);
        context.UserRoles.Add(new UserRole { UserId = lecturer.Id, User = lecturer, RoleId = lecturerRole.Id, Role = lecturerRole, AssignedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token;
        var semesterId = await GetSemesterIdAsync();
        using var request = CreatePreviewRequest(semesterId, CreateMentorWorkbook("forbidden-enterprise@example.com", "forbidden-academic@example.com"), token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminImport_ShouldCreateBothMentorTypes_AndAddThemToSelectedSemester()
    {
        var token = await GetAdminTokenAsync();
        var semesterId = await GetSemesterIdAsync();
        var enterpriseEmail = $"enterprise-{Guid.NewGuid():N}@example.com";
        var academicEmail = $"academic-{Guid.NewGuid():N}@example.edu.vn";
        using (var setupScope = factory.Services.CreateScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            setupContext.PendingRegistrations.Add(new PendingRegistration
            {
                FullName = "Pending Enterprise Mentor",
                Email = enterpriseEmail,
                NormalizedEmail = enterpriseEmail,
                PasswordHash = "not-used",
                RoleName = SystemRoles.Mentor,
                OtpHash = "not-used",
                OtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                Status = PendingRegistrationStatus.Pending
            });
            await setupContext.SaveChangesAsync();
        }
        using var previewRequest = CreatePreviewRequest(semesterId, CreateMentorWorkbook(enterpriseEmail, academicEmail), token);

        var previewResponse = await _client.SendAsync(previewRequest);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();

        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        preview!.Data!.CanCommit.Should().BeTrue();
        preview.Data.CreateCount.Should().Be(2);

        using var commitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/imports/commit")
        {
            Content = JsonContent.Create(new CommitMentorImportRequest { SessionId = preview.Data.SessionId })
        };
        commitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var commitResponse = await _client.SendAsync(commitRequest);
        var commit = await commitResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportCommitResponse>>();

        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        commit!.Data!.CreatedCount.Should().Be(2);
        commit.Data.SemesterAssignmentCount.Should().Be(2);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profiles = await context.MentorProfiles.AsNoTracking().Include(item => item.User)
            .Where(item => item.User.NormalizedEmail == enterpriseEmail || item.User.NormalizedEmail == academicEmail).ToListAsync();
        profiles.Should().ContainSingle(item => item.Type == MentorType.Enterprise && item.User.NormalizedEmail == enterpriseEmail && item.User.IsEmailVerified && item.FptEmail == null && item.DateOfBirth == new DateOnly(1993, 1, 21));
        profiles.Should().ContainSingle(item => item.Type == MentorType.Academic && item.User.NormalizedEmail == academicEmail && item.Department == "Bộ môn CNTT");
        var userIds = profiles.Select(item => item.UserId).ToArray();
        (await context.SemesterStaffAssignments.AsNoTracking().CountAsync(item => item.SemesterId == semesterId && userIds.Contains(item.UserId) && item.Role == SemesterStaffRole.Mentor && item.Status == SemesterStaffStatus.Active)).Should().Be(2);
        var pendingRegistration = await context.PendingRegistrations.AsNoTracking().SingleAsync(item => item.NormalizedEmail == enterpriseEmail);
        pendingRegistration.Status.Should().Be(PendingRegistrationStatus.Cancelled);
        pendingRegistration.CompletedUserId.Should().Be(profiles.Single(item => item.User.NormalizedEmail == enterpriseEmail).UserId);
    }

    [Fact]
    public async Task BalancedAllocation_ShouldFillBothSlotsAndKeepLoadsWithinOne()
    {
        var token = await GetAdminTokenAsync();
        Guid semesterId;
        Guid classId;
        Guid[] teamIds;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminId = await context.Users.Where(item => item.NormalizedEmail == "admin@ehub.test").Select(item => item.Id).SingleAsync();
            var mentorRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Mentor);
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var semester = new Semester { Code = $"AL{suffix}", Name = $"Allocation {suffix}", Term = SemesterTerm.Fall, Year = 2099, Status = SemesterStatus.Planned };
            var course = new Course { Code = $"C{suffix}", Name = "Allocation Course", Status = CourseStatus.Active };
            var targetClass = new Class { SemesterId = semester.Id, Semester = semester, CourseId = course.Id, Course = course, ClassCode = $"CL{suffix}", Slug = $"cl-{suffix}", ClassIndex = 1, Status = ClassStatus.Active, PrimaryLecturerId = adminId, ScheduleJson = "[{\"dayOfWeek\":1,\"startTime\":\"08:00\",\"endTime\":\"10:00\"}]", CreatedById = adminId };
            context.Semesters.Add(semester);
            context.Courses.Add(course);
            context.Classes.Add(targetClass);
            var teams = Enumerable.Range(1, 5).Select(index => new Team
            {
                ClassId = targetClass.Id, Class = targetClass, TeamCode = $"{targetClass.ClassCode}_T{index}", TeamName = $"Team {index}", Status = TeamStatus.Active, CreatedById = adminId
            }).ToArray();
            context.Teams.AddRange(teams);
            foreach (var type in new[] { MentorType.Enterprise, MentorType.Academic })
            {
                for (var index = 1; index <= 2; index++)
                {
                    var email = $"allocation-{type.ToString().ToLowerInvariant()}-{index}-{suffix}@example.com";
                    var user = new User { FullName = $"{type} Mentor {index}", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
                    context.Users.Add(user);
                    context.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = mentorRole.Id, Role = mentorRole, AssignedAt = DateTime.UtcNow, AssignedBy = adminId });
                    context.MentorProfiles.Add(new MentorProfile { UserId = user.Id, User = user, Type = type, Status = MentorProfileStatus.Active, CreatedBy = adminId });
                    context.SemesterStaffAssignments.Add(new SemesterStaffAssignment { SemesterId = semester.Id, Semester = semester, UserId = user.Id, User = user, Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active, CreatedBy = adminId });
                }
            }
            await context.SaveChangesAsync();
            semesterId = semester.Id;
            classId = targetClass.Id;
            teamIds = teams.Select(item => item.Id).ToArray();
        }

        using var previewRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/preview")
        {
            Content = JsonContent.Create(new PreviewMentorAllocationRequest { SemesterId = semesterId, ClassIds = [classId], Seed = 12345 })
        };
        previewRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var previewResponse = await _client.SendAsync(previewRequest);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>();

        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        preview!.Data!.CanCommit.Should().BeTrue();
        preview.Data.Assignments.Should().HaveCount(10);
        foreach (var type in new[] { MentorType.Enterprise.ToString(), MentorType.Academic.ToString() })
        {
            var loads = preview.Data.Assignments.Where(item => item.MentorType == type).GroupBy(item => item.MentorProfileId).Select(group => group.Count()).ToArray();
            loads.Max().Should().BeLessThanOrEqualTo(loads.Min() + 1);
        }

        using var commitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/commit")
        {
            Content = JsonContent.Create(new CommitMentorAllocationRequest { SessionId = preview.Data.SessionId })
        };
        commitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var commitResponse = await _client.SendAsync(commitRequest);
        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignments = await verifyContext.MentorAssignments.AsNoTracking()
            .Where(item => teamIds.Contains(item.TeamId) && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null).ToListAsync();
        assignments.Should().HaveCount(10);
        foreach (var teamId in teamIds)
            assignments.Where(item => item.TeamId == teamId).Select(item => item.Slot).Should().BeEquivalentTo([MentorType.Enterprise, MentorType.Academic]);
    }

    private async Task<Guid> GetSemesterIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Semesters.AsNoTracking().Select(item => item.Id).FirstAsync();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = "admin@ehub.test", Password = "Admin@123456" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    private static HttpRequestMessage CreatePreviewRequest(Guid semesterId, byte[] workbook, string? token = null)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(semesterId.ToString()), "semesterId");
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "mentors.xlsx");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/imports/preview") { Content = content };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static byte[] CreateMentorWorkbook(string enterpriseEmail, string academicEmail)
    {
        using var workbook = new XLWorkbook();
        var enterprise = workbook.Worksheets.Add("DS Mentor_FA26");
        string[] enterpriseHeaders = ["STT", "Họ và tên", "Ngày tháng năm sinh", "SDT", "Loại HĐ", "Trình độ học vấn", "Địa chỉ hiện nay", "Email", "Fpt Email", "Vị trí, Chức danh", "Công ty"];
        for (var index = 0; index < enterpriseHeaders.Length; index++) enterprise.Cell(1, index + 1).Value = enterpriseHeaders[index];
        string[] enterpriseValues = ["1", "Enterprise Integration Mentor", "21/01/1993", "0900000000", "Thỉnh giảng", "Thạc sĩ", "Đà Nẵng", enterpriseEmail, "", "CEO", "Integration Company"];
        for (var index = 0; index < enterpriseValues.Length; index++) enterprise.Cell(2, index + 1).Value = enterpriseValues[index];

        var academic = workbook.Worksheets.Add("Mentor IT_FA26");
        string[] academicHeaders = ["STT", "Email công việc", "Họ tên", "Phòng ban trực tiếp", "Chức danh (VN)"];
        for (var index = 0; index < academicHeaders.Length; index++) academic.Cell(1, index + 1).Value = academicHeaders[index];
        string[] academicValues = ["1", academicEmail, "Academic Integration Mentor", "Bộ môn CNTT", "Giảng viên"];
        for (var index = 0; index < academicValues.Length; index++) academic.Cell(2, index + 1).Value = academicValues[index];

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
