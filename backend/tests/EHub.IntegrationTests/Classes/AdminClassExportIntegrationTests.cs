using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Classes;
using EHub.Contracts.Common;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.IntegrationTests.Common;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EHub.IntegrationTests.Classes;

[Collection("Sequential")]
public sealed class AdminClassExportIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminClassExportIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AdminExport_OneSameSubjectAndMultipleSubjects_UsesOneSheetAndDeterministicOrder()
    {
        var seed = await CreateExportSeedAsync();
        var token = GenerateToken(seed.Admin, SystemRoles.Admin);

        using var oneClass = await ExportAsync(token, seed.SemesterCode, seed.Year, [seed.Classes[1].Id]);
        AssertWorkbook(oneClass, [seed.Classes[1].ClassCode]);

        using var sameSubject = await ExportAsync(
            token,
            seed.SemesterCode,
            seed.Year,
            [seed.Classes[2].Id, seed.Classes[0].Id, seed.Classes[1].Id]);
        AssertWorkbook(sameSubject, [
            seed.Classes[0].ClassCode,
            seed.Classes[1].ClassCode,
            seed.Classes[2].ClassCode
        ]);

        using var allClasses = await ExportAsync(
            token,
            seed.SemesterCode,
            seed.Year,
            [seed.Classes[3].Id, seed.Classes[2].Id, seed.Classes[1].Id, seed.Classes[0].Id]);
        AssertWorkbook(allClasses, seed.Classes.Select(item => item.ClassCode).ToArray());
    }

    [Fact]
    public async Task AdminExport_WhenClassIsOutsideRequestedSemester_RejectsEntireExport()
    {
        var seed = await CreateExportSeedAsync();
        var token = GenerateToken(seed.Admin, SystemRoles.Admin);

        using var request = AuthorizedPost(
            "/api/classes/bulk/export-excel",
            token,
            new ExportAdminClassDataRequest
            {
                Semester = seed.SemesterCode,
                Year = seed.Year,
                ClassIds = [seed.Classes[0].Id, seed.OutOfScopeClassId]
            });
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotBeNull();
        body!.Code.Should().Be(ErrorCodes.ClassValidationError);

        using var missingRequest = AuthorizedPost(
            "/api/classes/bulk/export-excel",
            token,
            new ExportAdminClassDataRequest
            {
                Semester = seed.SemesterCode,
                Year = seed.Year,
                ClassIds = [Guid.NewGuid()]
            });
        using var missingResponse = await _client.SendAsync(missingRequest);
        var missingBody = await missingResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();

        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingBody.Should().NotBeNull();
        missingBody!.Code.Should().Be(ErrorCodes.ClassNotFound);
    }

    [Fact]
    public async Task AdminExport_RequiresAuthenticationAndAdminRole()
    {
        var seed = await CreateExportSeedAsync();
        var payload = new ExportAdminClassDataRequest
        {
            Semester = seed.SemesterCode,
            Year = seed.Year,
            ClassIds = [seed.Classes[0].Id]
        };

        using var anonymousResponse = await _client.PostAsJsonAsync("/api/classes/bulk/export-excel", payload);
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var lecturerToken = GenerateToken(seed.Lecturer, SystemRoles.Lecturer);
        using var lecturerRequest = AuthorizedPost("/api/classes/bulk/export-excel", lecturerToken, payload);
        using var lecturerResponse = await _client.SendAsync(lecturerRequest);
        lecturerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LecturerSingleClassExport_StillUsesTheSharedRosterWorkbookFormat()
    {
        var seed = await CreateExportSeedAsync();
        var lecturerToken = GenerateToken(seed.Lecturer, SystemRoles.Lecturer);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/classes/{seed.Classes[0].Id}/export-excel?scope=Active&status=Active");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lecturerToken);

        using var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        workbook.Worksheets.Should().ContainSingle();
        var worksheet = workbook.Worksheet("Class Roster");
        worksheet.Cell(1, 1).GetString().Should().Be("RollNumber");
        worksheet.Cell(2, 5).GetString().Should().Be(seed.Classes[0].ClassCode);
    }

    private async Task<MemoryStream> ExportAsync(
        string token,
        string semester,
        int year,
        IReadOnlyCollection<Guid> classIds)
    {
        using var request = AuthorizedPost(
            "/api/classes/bulk/export-excel",
            token,
            new ExportAdminClassDataRequest
            {
                Semester = semester,
                Year = year,
                ClassIds = classIds
            });
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var downloadedFileName = response.Content.Headers.ContentDisposition!.FileNameStar
            ?? response.Content.Headers.ContentDisposition.FileName?.Trim('"');
        downloadedFileName.Should().Be($"{semester}{year}_class_data.xlsx");
        return new MemoryStream(await response.Content.ReadAsByteArrayAsync());
    }

    private static void AssertWorkbook(Stream stream, IReadOnlyCollection<string> expectedClassOrder)
    {
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);
        workbook.Worksheets.Should().ContainSingle();
        var worksheet = workbook.Worksheet("Class Roster");
        worksheet.Row(1).Cells(1, 11).Select(cell => cell.GetString()).Should().Equal(
            "RollNumber", "Fullname", "Chuyên ngành", "SubjectCode", "GroupName", "Group FA97",
            "Project Name", "Description", "Zalo Link", "Mentor", "Mentor - GV");
        worksheet.Range(2, 5, expectedClassOrder.Count + 1, 5)
            .Cells()
            .Select(cell => cell.GetString())
            .Should().Equal(expectedClassOrder);
    }

    private async Task<ExportSeed> CreateExportSeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await context.Users
            .Include(user => user.UserRoles)
            .ThenInclude(link => link.Role)
            .FirstAsync(user => user.UserRoles.Any(link => link.Role.Name == SystemRoles.Admin));
        var lecturer = await context.Users
            .Include(user => user.UserRoles)
            .ThenInclude(link => link.Role)
            .FirstOrDefaultAsync(user => user.UserRoles.Any(link => link.Role.Name == SystemRoles.Lecturer));
        var unique = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        if (lecturer is null)
        {
            var lecturerRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Lecturer);
            var lecturerEmail = $"export-lecturer-{unique}@example.com";
            lecturer = new User
            {
                FullName = $"Export Lecturer {unique}",
                Email = lecturerEmail,
                NormalizedEmail = lecturerEmail.ToLowerInvariant(),
                PasswordHash = "integration-test-only",
                Status = UserStatus.Active,
                IsEmailVerified = true
            };
            lecturer.UserRoles.Add(new UserRole
            {
                User = lecturer,
                UserId = lecturer.Id,
                Role = lecturerRole,
                RoleId = lecturerRole.Id
            });
            context.Users.Add(lecturer);
        }
        const int year = 2097;
        var semester = await context.Semesters.SingleOrDefaultAsync(item =>
            item.Term == SemesterTerm.Fall && item.Year == year);
        if (semester is null)
        {
            semester = new Semester
            {
                Code = $"FA{year}",
                Name = $"Fall {year}",
                Term = SemesterTerm.Fall,
                Year = year,
                Status = SemesterStatus.Planned,
                CreatedBy = admin.Id
            };
            context.Semesters.Add(semester);
        }

        var otherSemester = await context.Semesters.SingleOrDefaultAsync(item =>
            item.Term == SemesterTerm.Spring && item.Year == year);
        if (otherSemester is null)
        {
            otherSemester = new Semester
            {
                Code = $"SP{year}",
                Name = $"Spring {year}",
                Term = SemesterTerm.Spring,
                Year = year,
                Status = SemesterStatus.Planned,
                CreatedBy = admin.Id
            };
            context.Semesters.Add(otherSemester);
        }

        var exe101 = new Course
        {
            Code = $"X{unique}101",
            Name = $"Export Course 101 {unique}",
            Status = CourseStatus.Active,
            CreatedBy = admin.Id
        };
        var exe201 = new Course
        {
            Code = $"X{unique}201",
            Name = $"Export Course 201 {unique}",
            Status = CourseStatus.Active,
            CreatedBy = admin.Id
        };
        var classes = new[]
        {
            CreateClass(exe101, semester, 1, admin.Id, lecturer.Id),
            CreateClass(exe101, semester, 2, admin.Id, lecturer.Id),
            CreateClass(exe101, semester, 10, admin.Id, lecturer.Id),
            CreateClass(exe201, semester, 1, admin.Id, lecturer.Id)
        };
        var outOfScopeClass = CreateClass(exe201, otherSemester, 2, admin.Id, lecturer.Id);
        context.Courses.AddRange(exe101, exe201);
        context.Classes.AddRange(classes);
        context.Classes.Add(outOfScopeClass);

        for (var index = 0; index < classes.Length; index++)
        {
            var student = new Student
            {
                RollNumber = $"{unique}{index + 1:00}",
                NormalizedRollNumber = $"{unique}{index + 1:00}",
                FullName = $"Export Student {index + 1}",
                Email = $"export-{unique}-{index + 1}@example.com",
                MajorCode = "SE",
                Status = StudentStatus.Active,
                CreatedBy = admin.Id
            };
            context.Students.Add(student);
            context.ClassStudents.Add(new ClassStudent
            {
                Class = classes[index],
                ClassId = classes[index].Id,
                Student = student,
                StudentId = student.Id,
                SemesterId = semester.Id,
                CourseId = classes[index].CourseId,
                MajorCodeAtEnrollment = "SE",
                EnrollmentStatus = EnrollmentStatus.Active,
                CountsTowardCourseSemesterLimit = true
            });
        }

        await context.SaveChangesAsync();
        return new ExportSeed("FA", year, admin, lecturer, classes, outOfScopeClass.Id);
    }

    private static Class CreateClass(
        Course course,
        Semester semester,
        int classIndex,
        Guid adminId,
        Guid lecturerId) => new()
    {
        ClassCode = $"{course.Code}-{classIndex}",
        Slug = $"{semester.Code}-{course.Code}-{classIndex}".ToLowerInvariant(),
        ClassIndex = classIndex,
        Course = course,
        CourseId = course.Id,
        Semester = semester,
        SemesterId = semester.Id,
        PrimaryLecturerId = lecturerId,
        Status = ClassStatus.Draft,
        CreatedById = adminId,
        CreatedBy = adminId
    };

    private string GenerateToken(User user, string role)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(user, [role])
            .Token;
    }

    private static HttpRequestMessage AuthorizedPost(string url, string token, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed record ExportSeed(
        string SemesterCode,
        int Year,
        User Admin,
        User Lecturer,
        IReadOnlyList<Class> Classes,
        Guid OutOfScopeClassId);
}
