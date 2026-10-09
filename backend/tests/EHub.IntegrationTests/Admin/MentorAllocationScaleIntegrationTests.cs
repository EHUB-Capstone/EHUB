using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Admin;

/// <summary>
/// Runs the whole flow at the size of a real semester: 28 EXE101 teams and 79 EXE201 teams (107 in total),
/// 37 industry mentors and 17 lecturer mentors, the figures of the lecturer's sample tables.
/// </summary>
[Collection("Sequential")]
public sealed class MentorAllocationScaleIntegrationTests(CustomWebApplicationFactory factory)
{
    private const int Exe101Teams = 28;
    private const int Exe201Teams = 79;
    private const int EnterpriseMentors = 37;
    private const int AcademicMentors = 17;
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FullSemester_ShouldBeAssignedBalancedSavedAndExportedWithConsistentTotals()
    {
        var token = await GetAdminTokenAsync();
        var (semesterId, teamIds) = await SeedSemesterAsync();
        var total = Exe101Teams + Exe201Teams;

        var timer = Stopwatch.StartNew();
        using var previewRequest = Authorized(HttpMethod.Post, "/api/admin/mentors/allocations/preview", token,
            JsonContent.Create(new PreviewMentorAllocationRequest { SemesterId = semesterId, Seed = 2026 }));
        var previewResponse = await _client.SendAsync(previewRequest);
        var previewElapsed = timer.Elapsed;
        var preview = (await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;

        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        previewElapsed.Should().BeLessThan(MaximumDuration);
        preview.TeamCount.Should().Be(total);
        preview.Assignments.Should().HaveCount(total * 2, "every team needs one industry and one lecturer mentor");
        preview.Unfilled.Should().BeEmpty();
        preview.Assignments.GroupBy(item => (item.TeamId, item.MentorType)).Should().OnlyContain(group => group.Count() == 1);

        // 107 teams over 37 industry mentors is 2 or 3 each, and over 17 lecturers is 6 or 7 each, as in the sample tables.
        var enterpriseLoads = Loads(preview, nameof(MentorType.Enterprise));
        enterpriseLoads.Should().HaveCount(EnterpriseMentors).And.OnlyContain(count => count == 2 || count == 3);
        var academicLoads = Loads(preview, nameof(MentorType.Academic));
        academicLoads.Should().HaveCount(AcademicMentors).And.OnlyContain(count => count == 6 || count == 7);
        preview.MentorLoads.Should().HaveCount(EnterpriseMentors + AcademicMentors);
        preview.MentorLoads.Sum(item => item.TotalAfter).Should().Be(total * 2);

        timer.Restart();
        using var commitRequest = Authorized(HttpMethod.Post, "/api/admin/mentors/allocations/commit", token,
            JsonContent.Create(new CommitMentorAllocationRequest { SessionId = preview.SessionId }));
        var commitResponse = await _client.SendAsync(commitRequest);
        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        timer.Elapsed.Should().BeLessThan(MaximumDuration);

        using (var scope = factory.Services.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking()
                .Where(item => teamIds.Contains(item.TeamId) && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
                .ToListAsync();
            saved.Should().HaveCount(total * 2);
            saved.GroupBy(item => (item.TeamId, item.Slot)).Should().OnlyContain(group => group.Count() == 1);
        }

        timer.Restart();
        using var exportRequest = Authorized(HttpMethod.Get, $"/api/admin/mentors/assignments/export?semesterId={semesterId}", token);
        var exportResponse = await _client.SendAsync(exportRequest);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var workbook = new XLWorkbook(await exportResponse.Content.ReadAsStreamAsync());
        timer.Elapsed.Should().BeLessThan(MaximumDuration);

        workbook.Worksheets.Select(item => item.Name).Should().Equal("EXE101", "EXE201", "Tổng hợp");
        workbook.Worksheet("EXE101").RowsUsed().Count().Should().Be(Exe101Teams + 1);
        workbook.Worksheet("EXE201").RowsUsed().Count().Should().Be(Exe201Teams + 1);
        // Every team has both mentors, so no cell is left unassigned.
        workbook.Worksheets.Take(2).SelectMany(sheet => sheet.Column(10).CellsUsed().Skip(1).Concat(sheet.Column(11).CellsUsed().Skip(1)))
            .Select(cell => cell.GetString()).Should().NotContain("Chưa phân công");

        var summary = workbook.Worksheet("Tổng hợp");
        var totals = summary.Rows().Where(row => row.Cell(2).GetString() == "TỔNG").ToArray();
        totals.Should().HaveCount(2);
        foreach (var row in totals)
            row.Cells(3, 5).Select(cell => cell.GetString()).Should().Equal(Exe101Teams.ToString(), Exe201Teams.ToString(), total.ToString());
        // 29 mentors on "Thỉnh giảng" and 8 on "Khoán", like the sample sheet.
        summary.Column(6).CellsUsed().Select(cell => cell.GetString()).Count(value => value == "Thỉnh giảng").Should().Be(29);
        summary.Column(6).CellsUsed().Select(cell => cell.GetString()).Count(value => value == "Khoán").Should().Be(8);
    }

    private static int[] Loads(MentorAllocationPreviewResponse preview, string type) =>
        preview.MentorLoads.Where(item => item.MentorType == type).Select(item => item.TotalAfter).ToArray();

    private async Task<(Guid SemesterId, Guid[] TeamIds)> SeedSemesterAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminId = await context.Users.Where(item => item.NormalizedEmail == "admin@ehub.test").Select(item => item.Id).SingleAsync();
        var mentorRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Mentor);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var semester = new Semester { Code = $"SC{suffix}", Name = $"Scale {suffix}", Term = SemesterTerm.Spring, Year = 2074, Status = SemesterStatus.Planned };
        context.Semesters.Add(semester);
        Course CourseOf(string code) => context.Courses.Local.FirstOrDefault(item => item.Code == code)
            ?? context.Courses.SingleOrDefault(item => item.Code == code)
            ?? context.Courses.Add(new Course { Code = code, Name = code, Status = CourseStatus.Active }).Entity;
        var exe101 = CourseOf("EXE101");
        var exe201 = CourseOf("EXE201");

        var teams = new List<Team>();
        var classIndex = 0;
        void AddClass(Course course, int teamCount)
        {
            classIndex++;
            var @class = new Class
            {
                SemesterId = semester.Id, Semester = semester, CourseId = course.Id, Course = course, ClassCode = $"{course.Code}_{suffix}_{classIndex}",
                Slug = $"{course.Code}-{suffix}-{classIndex}".ToLowerInvariant(), ClassIndex = classIndex, Status = ClassStatus.Active, PrimaryLecturerId = adminId,
                ScheduleJson = "[{\"dayOfWeek\":1,\"startTime\":\"08:00\",\"endTime\":\"10:00\"}]", CreatedById = adminId
            };
            context.Classes.Add(@class);
            for (var number = 1; number <= teamCount; number++)
            {
                var team = new Team
                {
                    ClassId = @class.Id, Class = @class, TeamCode = $"{@class.ClassCode}_G{number}", TeamName = $"Team {classIndex}-{number}", Status = TeamStatus.Active, CreatedById = adminId
                };
                var roll = $"{suffix}{classIndex:00}{number:00}";
                var student = new Student { RollNumber = roll, NormalizedRollNumber = roll, FullName = $"Student {roll}", Email = $"{roll}@example.com".ToLowerInvariant(), MajorCode = "SE", Status = StudentStatus.Active, CreatedBy = adminId };
                var enrollment = new ClassStudent
                {
                    ClassId = @class.Id, Class = @class, StudentId = student.Id, Student = student, SemesterId = semester.Id, CourseId = course.Id,
                    MajorCodeAtEnrollment = "SE", EnrollmentStatus = EnrollmentStatus.Active, CountsTowardCourseSemesterLimit = true
                };
                team.TeamMembers.Add(new TeamMember { Team = team, TeamId = team.Id, ClassId = @class.Id, StudentId = student.Id, ClassStudent = enrollment, CountsTowardActiveTeam = true, CreatedById = adminId });
                context.Students.Add(student);
                context.ClassStudents.Add(enrollment);
                context.Teams.Add(team);
                teams.Add(team);
            }
        }

        AddClass(exe101, 14);
        AddClass(exe101, 14);
        AddClass(exe201, 20);
        AddClass(exe201, 20);
        AddClass(exe201, 20);
        AddClass(exe201, 19);

        void AddMentor(MentorType type, int number, string? contract)
        {
            var email = $"scale-{type.ToString().ToLowerInvariant()}-{number}-{suffix}@example.com";
            var user = new User { FullName = $"Scale {type} {number:00}", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = mentorRole.Id, Role = mentorRole, AssignedAt = DateTime.UtcNow, AssignedBy = adminId });
            context.MentorProfiles.Add(new MentorProfile { UserId = user.Id, User = user, Type = type, ContractType = contract, Status = MentorProfileStatus.Active, CreatedBy = adminId });
            context.SemesterStaffAssignments.Add(new SemesterStaffAssignment { SemesterId = semester.Id, Semester = semester, UserId = user.Id, User = user, Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active, CreatedBy = adminId });
        }

        for (var number = 1; number <= EnterpriseMentors; number++) AddMentor(MentorType.Enterprise, number, number <= 29 ? "Thỉnh giảng" : "Khoán");
        for (var number = 1; number <= AcademicMentors; number++) AddMentor(MentorType.Academic, number, null);

        await context.SaveChangesAsync();
        return (semester.Id, teams.Select(item => item.Id).ToArray());
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = "admin@ehub.test", Password = "Admin@123456" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }
}
