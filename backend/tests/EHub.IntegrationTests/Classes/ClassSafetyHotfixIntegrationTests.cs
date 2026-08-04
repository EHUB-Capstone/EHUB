using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Classes.AddStudentToClass;
using EHub.Application.Features.Classes.UpdateClass;
using EHub.Application.Features.Classes.UpdateClassSchedule;
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
public sealed class ClassSafetyHotfixIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ClassSafetyHotfixIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task InvalidSchedulePayload_Returns400ValidationError()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "http-validation");
        context.ChangeTracker.Clear();
        var targetClass = await context.Classes.SingleAsync(@class => @class.Id == seed.ClassId);
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var token = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);

        var request = CreateAuthorizedPutRequest(
            $"/api/classes/{seed.ClassId}/schedule",
            token,
            new UpdateClassScheduleRequest
            {
                RowVersion = targetClass.Version.ToString(),
                Schedules =
                [
                    new ClassScheduleSlotDto
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        SlotNumber = 0,
                        Room = "SH-101"
                    }
                ]
            });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotBeNull();
        body!.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Fact]
    public async Task UnassignedLecturerUpdatingSchedule_Returns403ClassAccessDenied()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "http-access");
        var unassignedLecturer = await CreateLecturerAsync(context, "http-unassigned");
        context.ChangeTracker.Clear();
        var targetClass = await context.Classes.SingleAsync(@class => @class.Id == seed.ClassId);
        var token = GenerateToken(scope.ServiceProvider, unassignedLecturer, SystemRoles.Lecturer);

        var request = CreateAuthorizedPutRequest(
            $"/api/classes/{seed.ClassId}/schedule",
            token,
            new UpdateClassScheduleRequest
            {
                RowVersion = targetClass.Version.ToString(),
                Schedules =
                [
                    new ClassScheduleSlotDto
                    {
                        DayOfWeek = DayOfWeek.Tuesday,
                        SlotNumber = 2,
                        Room = "SH-202"
                    }
                ]
            });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().NotBeNull();
        body!.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task ConflictingSchedule_Returns409ScheduleConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "http-conflict");
        await CreateConflictingClassAsync(context, seed);
        context.ChangeTracker.Clear();
        var targetClass = await context.Classes.SingleAsync(@class => @class.Id == seed.ClassId);
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var token = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);

        var request = CreateAuthorizedPutRequest(
            $"/api/classes/{seed.ClassId}/schedule",
            token,
            new UpdateClassScheduleRequest
            {
                RowVersion = targetClass.Version.ToString(),
                Schedules =
                [
                    new ClassScheduleSlotDto
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        SlotNumber = 1,
                        Room = "SH-101"
                    }
                ]
            });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Should().NotBeNull();
        body!.Code.Should().Be(ErrorCodes.ClassScheduleConflict);
    }

    [Fact]
    public async Task UpdatingSchedule_DoesNotChangeTeachingAssignment()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "schedule");
        context.ChangeTracker.Clear();

        var trackedClass = await context.Classes.SingleAsync(@class => @class.Id == seed.ClassId);
        var rowVersion = trackedClass.Version.ToString();
        context.ChangeTracker.Clear();

        var handler = new UpdateClassScheduleCommandHandler(context);
        var result = await handler.HandleAsync(
            seed.ClassId,
            new UpdateClassScheduleRequest
            {
                RowVersion = rowVersion,
                Schedules =
                [
                    new ClassScheduleSlotDto
                    {
                        DayOfWeek = DayOfWeek.Tuesday,
                        SlotNumber = 2,
                        Room = "SH-201"
                    }
                ]
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();
        var updatedClass = await context.Classes.AsNoTracking().SingleAsync(@class => @class.Id == seed.ClassId);
        updatedClass.PrimaryLecturerId.Should().Be(seed.LecturerId);
        updatedClass.ScheduleJson.Should().Contain("slotNumber");
    }

    [Fact]
    public async Task ReassigningLecturer_RevokesOldOwnership_PreservesSchedule_AndWritesAudit()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "assignment");
        var newLecturer = await CreateLecturerAsync(context, "new-assignment");
        var oldAssignment = await context.ClassLecturers.SingleAsync(assignment =>
            assignment.ClassId == seed.ClassId && assignment.LecturerId == seed.LecturerId);
        oldAssignment.IsPrimary = false;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var trackedClass = await context.Classes.SingleAsync(@class => @class.Id == seed.ClassId);
        var rowVersion = trackedClass.Version.ToString();
        var persistedScheduleJson = trackedClass.ScheduleJson;
        context.ChangeTracker.Clear();

        var handler = new UpdateClassCommandHandler(context);
        var result = await handler.UpdateTeachingAssignmentAsync(
            seed.ClassId,
            new UpdateTeachingAssignmentRequest
            {
                PrimaryLecturerId = newLecturer.Id,
                RowVersion = rowVersion
            },
            seed.AdminId,
            SystemRoles.Admin);

        result.IsSuccess.Should().BeTrue();
        context.ChangeTracker.Clear();

        var updatedClass = await context.Classes.AsNoTracking().SingleAsync(@class => @class.Id == seed.ClassId);
        var assignments = await context.ClassLecturers.AsNoTracking()
            .Where(assignment => assignment.ClassId == seed.ClassId)
            .ToListAsync();
        var audit = await context.ClassAuditLogs.AsNoTracking()
            .SingleAsync(log => log.ClassId == seed.ClassId && log.Action == "TEACHING_ASSIGNMENT_CHANGED");

        updatedClass.PrimaryLecturerId.Should().Be(newLecturer.Id);
        updatedClass.ScheduleJson.Should().Be(persistedScheduleJson);
        assignments.Should().ContainSingle(assignment => assignment.LecturerId == newLecturer.Id && assignment.IsPrimary);
        assignments.Should().NotContain(assignment => assignment.LecturerId == seed.LecturerId);
        audit.PerformedByUserId.Should().Be(seed.AdminId);
        audit.DetailsJson.Should().Contain(seed.LecturerId.ToString());
        audit.DetailsJson.Should().Contain(newLecturer.Id.ToString());
    }

    [Fact]
    public async Task AddingAlreadyEnrolledStudent_DoesNotMutateStudentProfile()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "student");
        var student = new Student
        {
            RollNumber = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            FullName = "Original Student Name",
            Email = $"original-{Guid.NewGuid():N}@example.com",
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        student.NormalizedRollNumber = student.RollNumber;
        context.Students.Add(student);
        context.ClassStudents.Add(new ClassStudent
        {
            ClassId = seed.ClassId,
            StudentId = student.Id,
            EnrollmentStatus = EnrollmentStatus.Active
        });
        await context.SaveChangesAsync();
        var originalName = student.FullName;
        var originalEmail = student.Email;
        context.ChangeTracker.Clear();

        var handler = new AddStudentToClassCommandHandler(context);
        var result = await handler.HandleAsync(
            seed.ClassId,
            new AddStudentToClassRequest
            {
                StudentCode = student.RollNumber!,
                FullName = "Mutated Name Must Not Persist",
                Email = $"mutated-{Guid.NewGuid():N}@example.com",
                MajorCode = MajorCodes.BIT_AI
            },
            seed.AdminId,
            SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassStudentAlreadyEnrolled);
        context.ChangeTracker.Clear();

        var unchangedStudent = await context.Students.AsNoTracking().SingleAsync(item => item.Id == student.Id);
        unchangedStudent.FullName.Should().Be(originalName);
        unchangedStudent.Email.Should().Be(originalEmail);
        unchangedStudent.MajorCode.Should().Be(MajorCodes.BIT_SE);
    }

    private static async Task<ClassSeed> CreateClassSeedAsync(AppDbContext context, string suffix)
    {
        var admin = await context.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstAsync(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin));
        var lecturer = await CreateLecturerAsync(context, suffix);
        var unique = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var course = new Course
        {
            Code = $"C{unique}",
            Name = $"Safety Course {unique}",
            Status = CourseStatus.Active,
            CreatedBy = admin.Id
        };
        var semester = new Semester
        {
            Code = $"S{unique}",
            Name = $"Safety Semester {unique}",
            Term = SemesterTerm.Fall,
            Year = 2030,
            Status = SemesterStatus.Active,
            CreatedBy = admin.Id
        };
        var scheduleJson = "[{\"dayOfWeek\":1,\"slotNumber\":1,\"room\":\"SH-101\"}]";
        var @class = new Class
        {
            ClassCode = $"{course.Code}_1",
            ClassIndex = 1,
            CourseId = course.Id,
            Course = course,
            SemesterId = semester.Id,
            Semester = semester,
            PrimaryLecturerId = lecturer.Id,
            PrimaryLecturer = lecturer,
            ScheduleJson = scheduleJson,
            Status = ClassStatus.Active,
            CreatedById = admin.Id,
            CreatedBy = admin.Id
        };

        context.Courses.Add(course);
        context.Semesters.Add(semester);
        context.Classes.Add(@class);
        context.ClassLecturers.Add(new ClassLecturer
        {
            ClassId = @class.Id,
            LecturerId = lecturer.Id,
            IsPrimary = true,
            AssignedById = admin.Id
        });
        await context.SaveChangesAsync();

        return new ClassSeed(@class.Id, admin.Id, lecturer.Id, scheduleJson);
    }

    private static async Task<User> CreateLecturerAsync(AppDbContext context, string suffix)
    {
        var lecturerRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Lecturer);
        var email = $"safety-{suffix}-{Guid.NewGuid():N}@example.com";
        var lecturer = new User
        {
            FullName = $"Safety Lecturer {suffix}",
            Email = email,
            NormalizedEmail = email.ToLowerInvariant(),
            PasswordHash = "integration-test-only",
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        lecturer.UserRoles.Add(new UserRole
        {
            UserId = lecturer.Id,
            RoleId = lecturerRole.Id,
            User = lecturer,
            Role = lecturerRole
        });
        context.Users.Add(lecturer);
        await context.SaveChangesAsync();
        return lecturer;
    }

    private static async Task CreateConflictingClassAsync(AppDbContext context, ClassSeed seed)
    {
        var targetClass = await context.Classes.AsNoTracking()
            .SingleAsync(@class => @class.Id == seed.ClassId);
        var unique = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var course = new Course
        {
            Code = $"C{unique}",
            Name = $"Conflict Course {unique}",
            Status = CourseStatus.Active,
            CreatedBy = seed.AdminId
        };
        var conflictingClass = new Class
        {
            ClassCode = $"{course.Code}_1",
            ClassIndex = 1,
            CourseId = course.Id,
            Course = course,
            SemesterId = targetClass.SemesterId,
            PrimaryLecturerId = seed.LecturerId,
            ScheduleJson = seed.ScheduleJson,
            Status = ClassStatus.Active,
            CreatedById = seed.AdminId,
            CreatedBy = seed.AdminId
        };
        context.Courses.Add(course);
        context.Classes.Add(conflictingClass);
        context.ClassLecturers.Add(new ClassLecturer
        {
            ClassId = conflictingClass.Id,
            LecturerId = seed.LecturerId,
            IsPrimary = true,
            AssignedById = seed.AdminId
        });
        await context.SaveChangesAsync();
    }

    private static string GenerateToken(IServiceProvider services, User user, string role) =>
        services.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(user, [role])
            .Token;

    private static HttpRequestMessage CreateAuthorizedPutRequest(string url, string token, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private sealed record ClassSeed(Guid ClassId, Guid AdminId, Guid LecturerId, string ScheduleJson);
}
