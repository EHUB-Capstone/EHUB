using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.AddStudentToClass;
using EHub.Application.Features.Classes.CreateClass;
using EHub.Application.Features.Classes.Common;
using EHub.Application.Features.Classes.ImportStudents;
using EHub.Application.Features.Classes.UpdateClass;
using EHub.Application.Features.Classes.UpdateClassSchedule;
using EHub.Contracts.Classes;
using EHub.Contracts.Common;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.IntegrationTests.Common;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task ClassList_ReturnsAllClassesForAdmin_ButOnlyAssignedClassesForLecturer()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "class-list-ownership");
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var assignedLecturer = await context.Users.SingleAsync(user => user.Id == seed.LecturerId);
        var otherLecturer = await CreateLecturerAsync(context, "class-list-other");
        var sourceClass = await context.Classes.AsNoTracking()
            .SingleAsync(@class => @class.Id == seed.ClassId);
        var otherClass = new Class
        {
            ClassCode = $"{sourceClass.ClassCode}_2",
            ClassIndex = sourceClass.ClassIndex + 1,
            CourseId = sourceClass.CourseId,
            SemesterId = sourceClass.SemesterId,
            PrimaryLecturerId = otherLecturer.Id,
            ScheduleJson = sourceClass.ScheduleJson,
            Status = ClassStatus.Active,
            CreatedById = seed.AdminId,
            CreatedBy = seed.AdminId
        };
        context.Classes.Add(otherClass);
        context.ClassLecturers.Add(new ClassLecturer
        {
            ClassId = otherClass.Id,
            LecturerId = otherLecturer.Id,
            IsPrimary = true,
            AssignedById = seed.AdminId
        });
        await context.SaveChangesAsync();

        var adminToken = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);
        var assignedToken = GenerateToken(scope.ServiceProvider, assignedLecturer, SystemRoles.Lecturer);
        var otherToken = GenerateToken(scope.ServiceProvider, otherLecturer, SystemRoles.Lecturer);

        var adminResult = await GetClassListAsync(adminToken);
        adminResult.TotalCount.Should().Be(2);
        adminResult.Items.Should().HaveCount(2);
        adminResult.Items.Should().Contain(item => item.Id == seed.ClassId);
        adminResult.Items.Should().Contain(item => item.Id == otherClass.Id);

        var assignedResult = await GetClassListAsync(assignedToken);
        assignedResult.TotalCount.Should().Be(1);
        assignedResult.Items.Should().ContainSingle();
        assignedResult.Items.Should().Contain(item => item.Id == seed.ClassId);
        assignedResult.Items.Should().NotContain(item => item.Id == otherClass.Id);
        assignedResult.Items.Should().OnlyContain(item => item.PrimaryLecturerId == seed.LecturerId);

        var otherResult = await GetClassListAsync(otherToken);
        otherResult.TotalCount.Should().Be(1);
        otherResult.Items.Should().ContainSingle();
        otherResult.Items.Should().Contain(item => item.Id == otherClass.Id);
        otherResult.Items.Should().NotContain(item => item.Id == seed.ClassId);
        otherResult.Items.Should().OnlyContain(item => item.PrimaryLecturerId == otherLecturer.Id);

        async Task<ClassListResponse> GetClassListAsync(string token)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/classes?page=1&pageSize=100&status=Active&semesterId={sourceClass.SemesterId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<ClassListResponse>>();
            body.Should().NotBeNull();
            body!.Success.Should().BeTrue();
            body.Data.Should().NotBeNull();
            return body.Data!;
        }
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

        var handler = new UpdateClassScheduleCommandHandler(
            context,
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
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
    public async Task AdminCreatedAssignedClass_StaysDraftUntilAssignedLecturerAddsSchedule()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "draft-lifecycle");
        var sourceClass = await context.Classes.AsNoTracking().SingleAsync(@class => @class.Id == seed.ClassId);
        var nextIndex = 2;
        while (await context.Classes.AnyAsync(@class =>
                   @class.SemesterId == sourceClass.SemesterId &&
                   @class.CourseId == sourceClass.CourseId &&
                   @class.ClassIndex == nextIndex))
        {
            nextIndex++;
        }

        var createHandler = new CreateClassCommandHandler(context);
        var createResult = await createHandler.HandleAsync(
            new CreateClassRequest
            {
                CourseId = sourceClass.CourseId,
                SemesterId = sourceClass.SemesterId,
                ClassIndex = nextIndex,
                PrimaryLecturerId = seed.LecturerId
            },
            seed.AdminId,
            SystemRoles.Admin);

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.Status.Should().Be(nameof(ClassStatus.Draft));
        createResult.Value.PrimaryLecturerId.Should().Be(seed.LecturerId);
        context.ChangeTracker.Clear();

        var draft = await context.Classes.SingleAsync(@class => @class.Id == createResult.Value.Id);
        var scheduleHandler = new UpdateClassScheduleCommandHandler(
            context,
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        var scheduleResult = await scheduleHandler.HandleAsync(
            draft.Id,
            new UpdateClassScheduleRequest
            {
                RowVersion = draft.Version.ToString(),
                Schedules =
                [
                    new ClassScheduleSlotDto
                    {
                        DayOfWeek = DayOfWeek.Saturday,
                        SlotNumber = 4,
                        Room = $"DRAFT-{nextIndex}"
                    }
                ]
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        scheduleResult.IsSuccess.Should().BeTrue();
        scheduleResult.Value.Status.Should().Be(nameof(ClassStatus.Active));
    }

    [Fact]
    public async Task AdminCanCreateUnassignedClassThenAssignItToLecturer()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "create-then-assign");
        var sourceClass = await context.Classes.AsNoTracking().SingleAsync(@class => @class.Id == seed.ClassId);
        var nextIndex = 2;
        while (await context.Classes.AnyAsync(@class =>
                   @class.SemesterId == sourceClass.SemesterId &&
                   @class.CourseId == sourceClass.CourseId &&
                   @class.ClassIndex == nextIndex))
        {
            nextIndex++;
        }

        var createResult = await new CreateClassCommandHandler(context).HandleAsync(
            new CreateClassRequest
            {
                CourseId = sourceClass.CourseId,
                SemesterId = sourceClass.SemesterId,
                ClassIndex = nextIndex,
                PrimaryLecturerId = null
            },
            seed.AdminId,
            SystemRoles.Admin);

        createResult.IsSuccess.Should().BeTrue();
        createResult.Value.Status.Should().Be(nameof(ClassStatus.Draft));
        createResult.Value.PrimaryLecturerId.Should().BeNull();
        context.ChangeTracker.Clear();

        var assignResult = await new UpdateClassCommandHandler(
                context,
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>())
            .UpdateTeachingAssignmentAsync(
                createResult.Value.Id,
                new UpdateTeachingAssignmentRequest
                {
                    PrimaryLecturerId = seed.LecturerId,
                    RowVersion = createResult.Value.RowVersion
                },
                seed.AdminId,
                SystemRoles.Admin);

        assignResult.IsSuccess.Should().BeTrue();
        assignResult.Value.PrimaryLecturerId.Should().Be(seed.LecturerId);
        assignResult.Value.Status.Should().Be(nameof(ClassStatus.Draft));
        context.ChangeTracker.Clear();
        var persistedClass = await context.Classes.AsNoTracking()
            .SingleAsync(@class => @class.Id == createResult.Value.Id);
        persistedClass.PrimaryLecturerId.Should().Be(seed.LecturerId);
        (await context.ClassLecturers.AsNoTracking().CountAsync(assignment =>
            assignment.ClassId == createResult.Value.Id &&
            assignment.LecturerId == seed.LecturerId &&
            assignment.IsPrimary)).Should().Be(1);
    }

    [Fact]
    public async Task LecturerCannotCreateClassesThroughSingleOrBulkEndpoints()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "admin-only-create");
        var sourceClass = await context.Classes.AsNoTracking().SingleAsync(@class => @class.Id == seed.ClassId);
        var lecturer = await context.Users.SingleAsync(user => user.Id == seed.LecturerId);
        var token = GenerateToken(scope.ServiceProvider, lecturer, SystemRoles.Lecturer);
        var nextIndex = 2;
        while (await context.Classes.AnyAsync(@class =>
                   @class.SemesterId == sourceClass.SemesterId &&
                   @class.CourseId == sourceClass.CourseId &&
                   @class.ClassIndex == nextIndex))
        {
            nextIndex++;
        }

        using var singleRequest = CreateAuthorizedPostRequest(
            "/api/classes",
            token,
            new CreateClassRequest
            {
                CourseId = sourceClass.CourseId,
                SemesterId = sourceClass.SemesterId,
                ClassIndex = nextIndex
            });
        var singleResponse = await _client.SendAsync(singleRequest);

        using var bulkPreviewRequest = CreateAuthorizedPostRequest(
            "/api/classes/bulk/preview",
            token,
            new CreateBulkClassesRequest
            {
                CourseId = sourceClass.CourseId,
                SemesterId = sourceClass.SemesterId,
                ClassIndices = [nextIndex]
            });
        var bulkPreviewResponse = await _client.SendAsync(bulkPreviewRequest);

        singleResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        bulkPreviewResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        context.ChangeTracker.Clear();
        (await context.Classes.AsNoTracking().AnyAsync(@class =>
            @class.SemesterId == sourceClass.SemesterId &&
            @class.CourseId == sourceClass.CourseId &&
            @class.ClassIndex == nextIndex)).Should().BeFalse();
    }

    [Fact]
    public async Task DatabaseRejectsTwoCountedEnrollmentsForSameStudentCourseAndSemester()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "enrollment-unique");
        var sourceClass = await context.Classes.AsNoTracking().SingleAsync(@class => @class.Id == seed.ClassId);
        var secondClass = new Class
        {
            ClassCode = $"{sourceClass.ClassCode}_ALT_{Guid.NewGuid():N}"[..Math.Min(50, sourceClass.ClassCode.Length + 13)],
            ClassIndex = 99,
            CourseId = sourceClass.CourseId,
            SemesterId = sourceClass.SemesterId,
            PrimaryLecturerId = seed.LecturerId,
            Status = ClassStatus.Draft,
            CreatedById = seed.AdminId,
            CreatedBy = seed.AdminId
        };
        var studentCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var student = new Student
        {
            RollNumber = studentCode,
            NormalizedRollNumber = studentCode,
            FullName = "Unique Enrollment Student",
            Email = $"unique-{Guid.NewGuid():N}@example.com",
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        context.Classes.Add(secondClass);
        context.ClassLecturers.Add(new ClassLecturer
        {
            ClassId = secondClass.Id,
            LecturerId = seed.LecturerId,
            IsPrimary = true,
            AssignedById = seed.AdminId
        });
        context.Students.Add(student);
        context.ClassStudents.Add(new ClassStudent
        {
            ClassId = seed.ClassId,
            StudentId = student.Id,
            SemesterId = sourceClass.SemesterId,
            CourseId = sourceClass.CourseId,
            EnrollmentStatus = EnrollmentStatus.Active,
            CountsTowardCourseSemesterLimit = true,
            MajorCodeAtEnrollment = MajorCodes.BIT_SE
        });
        await context.SaveChangesAsync();

        context.ClassStudents.Add(new ClassStudent
        {
            ClassId = secondClass.Id,
            StudentId = student.Id,
            SemesterId = sourceClass.SemesterId,
            CourseId = sourceClass.CourseId,
            EnrollmentStatus = EnrollmentStatus.Active,
            CountsTowardCourseSemesterLimit = true,
            MajorCodeAtEnrollment = MajorCodes.BIT_SE
        });

        var save = () => context.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task ReassigningLecturer_RevokesOldOwnership_PreservesSchedule_AndWritesAudit()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "assignment");
        var newLecturer = await CreateLecturerAsync(context, "new-assignment");
        context.ChangeTracker.Clear();

        var trackedClass = await context.Classes.SingleAsync(@class => @class.Id == seed.ClassId);
        var rowVersion = trackedClass.Version.ToString();
        var persistedScheduleJson = trackedClass.ScheduleJson;
        context.ChangeTracker.Clear();

        var handler = new UpdateClassCommandHandler(
            context,
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
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
    public async Task UnassigningActiveClass_Returns409AndKeepsLecturerOwnership()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "active-unassign");
        context.ChangeTracker.Clear();
        var targetClass = await context.Classes.SingleAsync(@class => @class.Id == seed.ClassId);
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var token = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);

        var request = CreateAuthorizedPutRequest(
            $"/api/classes/{seed.ClassId}/teaching-assignment",
            token,
            new UpdateTeachingAssignmentRequest
            {
                PrimaryLecturerId = null,
                RowVersion = targetClass.Version.ToString()
            });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body!.Code.Should().Be(ErrorCodes.ClassLecturerRequired);
        context.ChangeTracker.Clear();
        (await context.Classes.AsNoTracking().SingleAsync(@class => @class.Id == seed.ClassId))
            .PrimaryLecturerId.Should().Be(seed.LecturerId);
        (await context.ClassLecturers.AsNoTracking().CountAsync(assignment => assignment.ClassId == seed.ClassId))
            .Should().Be(1);
    }

    [Fact]
    public async Task AddingExistingStudent_WithoutMajor_UsesRegisteredProfileMajor()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "major-snapshot");
        var studentCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var student = new Student
        {
            RollNumber = studentCode,
            NormalizedRollNumber = studentCode,
            FullName = "Profile Source Of Truth",
            Email = $"snapshot-{Guid.NewGuid():N}@example.com",
            MajorCode = MajorCodes.BIT_AI,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var handler = new AddStudentToClassCommandHandler(context);
        var result = await handler.HandleAsync(
            seed.ClassId,
            new AddStudentToClassRequest
            {
                StudentCode = studentCode,
                FullName = "Attempted Profile Overwrite",
                Email = student.Email!,
                MajorCode = null
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        result.Value.MajorCode.Should().Be(MajorCodes.BIT_AI);
        result.Value.ProfileMajorCode.Should().Be(MajorCodes.BIT_AI);
        context.ChangeTracker.Clear();

        var persistedProfile = await context.Students.AsNoTracking().SingleAsync(item => item.Id == student.Id);
        var enrollment = await context.ClassStudents.AsNoTracking()
            .SingleAsync(item => item.ClassId == seed.ClassId && item.StudentId == student.Id);
        persistedProfile.FullName.Should().Be("Profile Source Of Truth");
        persistedProfile.MajorCode.Should().Be(MajorCodes.BIT_AI);
        enrollment.MajorCodeAtEnrollment.Should().Be(MajorCodes.BIT_AI);
        enrollment.MajorVerificationStatus.Should().Be(EnrollmentMajorVerificationStatus.Unverified);
    }

    [Fact]
    public async Task AddingExistingStudent_WithDifferentMajor_ReturnsSpecificMismatchWithoutEnrollment()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "major-mismatch");
        var studentCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var student = new Student
        {
            RollNumber = studentCode,
            NormalizedRollNumber = studentCode,
            FullName = "Registered Major Student",
            Email = $"major-mismatch-{Guid.NewGuid():N}@example.com",
            MajorCode = MajorCodes.BIT_AI,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new AddStudentToClassCommandHandler(context).HandleAsync(
            seed.ClassId,
            new AddStudentToClassRequest
            {
                StudentCode = studentCode,
                FullName = student.FullName,
                Email = student.Email!,
                MajorCode = MajorCodes.BIT_SE
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassStudentMajorMismatch);
        result.Error.Message.Should().Contain(MajorCodes.BIT_AI);
        context.ChangeTracker.Clear();
        (await context.ClassStudents.AsNoTracking().AnyAsync(item =>
            item.ClassId == seed.ClassId && item.StudentId == student.Id)).Should().BeFalse();
        (await context.Students.AsNoTracking().SingleAsync(item => item.Id == student.Id)).MajorCode
            .Should().Be(MajorCodes.BIT_AI);
    }

    [Fact]
    public async Task AddingStudent_WhenCodeAndEmailBelongToDifferentProfiles_ReturnsSpecificIdentityConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "student-identity-conflict");
        var codeOwnerCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var emailOwnerCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var codeOwner = new Student
        {
            RollNumber = codeOwnerCode,
            NormalizedRollNumber = codeOwnerCode,
            FullName = "Code Owner",
            Email = $"code-owner-{Guid.NewGuid():N}@example.com",
            MajorCode = MajorCodes.BIT_AI,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        var emailOwner = new Student
        {
            RollNumber = emailOwnerCode,
            NormalizedRollNumber = emailOwnerCode,
            FullName = "Email Owner",
            Email = $"email-owner-{Guid.NewGuid():N}@example.com",
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        context.Students.AddRange(codeOwner, emailOwner);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new AddStudentToClassCommandHandler(context).HandleAsync(
            seed.ClassId,
            new AddStudentToClassRequest
            {
                StudentCode = codeOwnerCode,
                FullName = codeOwner.FullName,
                Email = emailOwner.Email!,
                MajorCode = null
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassStudentIdentityConflict);
        result.Error.Message.Should().Contain(codeOwnerCode);
        result.Error.Message.Should().Contain(emailOwner.Email!);
        context.ChangeTracker.Clear();
        (await context.ClassStudents.AsNoTracking().AnyAsync(item => item.ClassId == seed.ClassId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task AddingNewStudent_WithoutMajor_ReturnsValidationErrorWithoutCreatingProfile()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "new-student-major-required");
        var studentCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var email = $"new-student-{Guid.NewGuid():N}@example.com";

        var result = await new AddStudentToClassCommandHandler(context).HandleAsync(
            seed.ClassId,
            new AddStudentToClassRequest
            {
                StudentCode = studentCode,
                FullName = "New Student",
                Email = email,
                MajorCode = null
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
        result.Error.Message.Should().Be("Major is required when creating a new student profile.");
        context.ChangeTracker.Clear();
        (await context.Students.AsNoTracking().AnyAsync(item => item.NormalizedRollNumber == studentCode)).Should().BeFalse();
    }

    [Fact]
    public async Task AssignedLecturerCanPreviewAndCommitImport_AndSessionCannotBeReused()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "lecturer-import");
        var lecturer = await context.Users.SingleAsync(user => user.Id == seed.LecturerId);
        var token = GenerateToken(scope.ServiceProvider, lecturer, SystemRoles.Lecturer);
        var studentCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var studentEmail = $"import-{Guid.NewGuid():N}@example.com";

        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreateImportWorkbook(studentCode, studentEmail, MajorCodes.BIT_SE));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(fileContent, "file", "students.xlsx");
        using var previewRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/classes/{seed.ClassId}/import-students/preview")
        {
            Content = multipart
        };
        previewRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var previewResponse = await _client.SendAsync(previewRequest);
        var previewBody = await previewResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStudentsPreviewResponse>>();

        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        previewBody!.Data!.SessionId.Should().NotBeEmpty();
        previewBody.Data.ValidRowsCount.Should().Be(1);

        using var commitRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/classes/{seed.ClassId}/import-students/commit")
        {
            Content = JsonContent.Create(new CommitImportStudentsRequest { SessionId = previewBody.Data.SessionId })
        };
        commitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var commitResponse = await _client.SendAsync(commitRequest);
        var commitBody = await commitResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStudentsCommitResponse>>();

        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        commitBody!.Data!.InsertedCount.Should().Be(1);
        commitBody.Data.ErrorCount.Should().Be(0);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/classes/{seed.ClassId}/import-students/commit")
        {
            Content = JsonContent.Create(new CommitImportStudentsRequest { SessionId = previewBody.Data.SessionId })
        };
        replayRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var replayResponse = await _client.SendAsync(replayRequest);
        var replayBody = await replayResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();

        replayResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replayBody!.Code.Should().Be(ErrorCodes.ClassImportSessionInvalid);
        context.ChangeTracker.Clear();

        var importedProfile = await context.Students.AsNoTracking()
            .SingleAsync(student => student.NormalizedRollNumber == studentCode);
        var enrollment = await context.ClassStudents.AsNoTracking()
            .SingleAsync(item => item.ClassId == seed.ClassId && item.StudentId == importedProfile.Id);
        enrollment.MajorCodeAtEnrollment.Should().Be(MajorCodes.BIT_SE);
        (await context.ClassImportSessions.AsNoTracking().SingleAsync(item => item.Id == previewBody.Data.SessionId))
            .Status.Should().Be(ClassImportSessionStatus.Consumed);
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
            SemesterId = (await context.Classes.AsNoTracking().SingleAsync(@class => @class.Id == seed.ClassId)).SemesterId,
            CourseId = (await context.Classes.AsNoTracking().SingleAsync(@class => @class.Id == seed.ClassId)).CourseId,
            EnrollmentStatus = EnrollmentStatus.Active,
            CountsTowardCourseSemesterLimit = true,
            MajorCodeAtEnrollment = MajorCodes.BIT_SE
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
                Email = student.Email!,
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

    [Fact]
    public async Task ImportSystemFailure_RollsBackProfilesEnrollmentsAuditAndOutbox()
    {
        using var scope = _factory.Services.CreateScope();
        var seedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(seedContext, "import-rollback");
        var studentCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var email = $"rollback-{Guid.NewGuid():N}@example.com";
        var session = new ClassImportSession
        {
            Id = Guid.NewGuid(),
            ClassId = seed.ClassId,
            UserId = seed.AdminId,
            Status = ClassImportSessionStatus.Available,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            ValidRowsJson = JsonSerializer.Serialize(new[]
            {
                new ImportStudentRowPreviewDto
                {
                    RowNumber = 2,
                    StudentCode = studentCode,
                    FullName = "Rollback Student",
                    Email = email,
                    MajorCode = MajorCodes.BIT_SE,
                    IsValid = true,
                    Status = "Valid"
                }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        seedContext.ClassImportSessions.Add(session);
        await seedContext.SaveChangesAsync();
        seedContext.ChangeTracker.Clear();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(seedContext.Database.GetConnectionString())
            .AddInterceptors(new ThrowWhenEnrollmentIsInsertedInterceptor())
            .Options;
        await using var failingContext = new AppDbContext(options);
        var handler = new CommitImportStudentsCommandHandler(
            failingContext,
            new EHub.Infrastructure.Persistence.UnitOfWork(failingContext));

        var result = await handler.HandleAsync(
            seed.ClassId,
            new CommitImportStudentsRequest { SessionId = session.Id },
            seed.AdminId,
            SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassStudentEnrollmentConflict);
        seedContext.ChangeTracker.Clear();
        (await seedContext.Students.AsNoTracking().AnyAsync(student => student.NormalizedRollNumber == studentCode))
            .Should().BeFalse();
        (await seedContext.ClassStudents.AsNoTracking().AnyAsync(enrollment => enrollment.ClassId == seed.ClassId))
            .Should().BeFalse();
        (await seedContext.ClassAuditLogs.AsNoTracking().AnyAsync(log =>
            log.ClassId == seed.ClassId && log.Action == "STUDENT_IMPORT_COMMITTED"))
            .Should().BeFalse();
        (await seedContext.OutboxMessages.AsNoTracking().AnyAsync(message => message.AggregateId == seed.ClassId))
            .Should().BeFalse();
        (await seedContext.ClassImportSessions.AsNoTracking().SingleAsync(item => item.Id == session.Id))
            .Status.Should().Be(ClassImportSessionStatus.Available);
    }

    [Fact]
    public async Task ConcurrentEnrollment_OnlyOneClassCanCountForTheCourseAndSemester()
    {
        using var scope = _factory.Services.CreateScope();
        var seedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(seedContext, "concurrent-enrollment");
        var siblingClassId = await CreateSiblingClassAsync(seedContext, seed);
        var studentCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var student = new Student
        {
            RollNumber = studentCode,
            NormalizedRollNumber = studentCode,
            FullName = "Concurrent Student",
            Email = $"concurrent-{Guid.NewGuid():N}@example.com",
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        seedContext.Students.Add(student);
        await seedContext.SaveChangesAsync();
        seedContext.ChangeTracker.Clear();

        var connectionString = seedContext.Database.GetConnectionString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var firstContext = new AppDbContext(options);
        await using var secondContext = new AppDbContext(options);
        var request = new AddStudentToClassRequest
        {
            StudentCode = studentCode,
            FullName = student.FullName,
            Email = student.Email!,
            MajorCode = MajorCodes.BIT_SE
        };

        var results = await Task.WhenAll(
            new AddStudentToClassCommandHandler(firstContext).HandleAsync(
                seed.ClassId, request, seed.AdminId, SystemRoles.Admin),
            new AddStudentToClassCommandHandler(secondContext).HandleAsync(
                siblingClassId, request, seed.AdminId, SystemRoles.Admin));

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => result.IsFailure && result.Error.Code == ErrorCodes.ClassStudentEnrollmentConflict)
            .Should().Be(1);
        seedContext.ChangeTracker.Clear();
        (await seedContext.ClassStudents.AsNoTracking().CountAsync(enrollment =>
            enrollment.StudentId == student.Id && enrollment.CountsTowardCourseSemesterLimit))
            .Should().Be(1);
    }

    [Fact]
    public async Task ArchiveAndRestore_PreserveData_WriteAudit_AndAreIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "lifecycle-roundtrip");
        context.ChangeTracker.Clear();
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var token = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);
        var version = (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Version.ToString();
        var archivePayload = new ChangeClassLifecycleRequest { RowVersion = version, Reason = "End of local test cycle" };

        using var archiveRequest = CreateAuthorizedPostRequest($"/api/classes/{seed.ClassId}/archive", token, archivePayload);
        var archiveResponse = await _client.SendAsync(archiveRequest);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var repeatedArchiveRequest = CreateAuthorizedPostRequest($"/api/classes/{seed.ClassId}/archive", token, archivePayload);
        (await _client.SendAsync(repeatedArchiveRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        context.ChangeTracker.Clear();
        var archived = await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId);
        archived.Status.Should().Be(ClassStatus.Archived);
        ClassScheduleRules.Deserialize(archived.ScheduleJson).Should().BeEquivalentTo(
            ClassScheduleRules.Deserialize(seed.ScheduleJson));
        archived.StatusBeforeArchive.Should().Be(ClassStatus.Active);
        (await context.ClassAuditLogs.CountAsync(item => item.ClassId == seed.ClassId && item.Action == "CLASS_ARCHIVED")).Should().Be(1);

        using var restoreRequest = CreateAuthorizedPostRequest($"/api/classes/{seed.ClassId}/restore", token,
            new ChangeClassLifecycleRequest { RowVersion = archived.Version.ToString(), Reason = "Continue the class" });
        (await _client.SendAsync(restoreRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        context.ChangeTracker.Clear();
        var restored = await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId);
        restored.Status.Should().Be(ClassStatus.Active);
        restored.ArchivedAtUtc.Should().BeNull();
        restored.StatusBeforeArchive.Should().BeNull();
        (await context.ClassAuditLogs.CountAsync(item => item.ClassId == seed.ClassId && item.Action == "CLASS_RESTORED")).Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentRestore_ProducesOneStateTransitionAndOneAuditRecord()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "lifecycle-concurrency");
        context.ChangeTracker.Clear();
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var token = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);
        var initialVersion = (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Version.ToString();
        using var archiveRequest = CreateAuthorizedPostRequest($"/api/classes/{seed.ClassId}/archive", token,
            new ChangeClassLifecycleRequest { RowVersion = initialVersion, Reason = "Prepare restore concurrency test" });
        (await _client.SendAsync(archiveRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        context.ChangeTracker.Clear();
        var archivedVersion = (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Version.ToString();
        var payload = new ChangeClassLifecycleRequest { RowVersion = archivedVersion, Reason = "Concurrent restore request" };

        using var firstRequest = CreateAuthorizedPostRequest($"/api/classes/{seed.ClassId}/restore", token, payload);
        using var secondRequest = CreateAuthorizedPostRequest($"/api/classes/{seed.ClassId}/restore", token, payload);
        var responses = await Task.WhenAll(_client.SendAsync(firstRequest), _client.SendAsync(secondRequest));
        var responseBodies = await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()));

        responses.Should().OnlyContain(response =>
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict,
            "lifecycle concurrency must be mapped to a safe response; bodies were: {0}", string.Join(" | ", responseBodies));
        context.ChangeTracker.Clear();
        (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Status.Should().Be(ClassStatus.Active);
        (await context.ClassAuditLogs.CountAsync(item => item.ClassId == seed.ClassId && item.Action == "CLASS_RESTORED")).Should().Be(1);
    }

    [Fact]
    public async Task ChatRepair_AllowsAssignedLecturer_RejectsOtherLecturer_IsIdempotent_AndFollowsArchiveState()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "chat-repair");
        context.ChangeTracker.Clear();
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var lecturer = await context.Users.SingleAsync(user => user.Id == seed.LecturerId);
        var otherLecturer = await CreateLecturerAsync(context, "other-chat-repair");
        var adminToken = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);
        var lecturerToken = GenerateToken(scope.ServiceProvider, lecturer, SystemRoles.Lecturer);
        var otherLecturerToken = GenerateToken(scope.ServiceProvider, otherLecturer, SystemRoles.Lecturer);

        using var forbiddenRequest = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/repair-chat-memberships", otherLecturerToken, new { });
        (await _client.SendAsync(forbiddenRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var repairRequest = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/repair-chat-memberships", lecturerToken, new { });
        (await _client.SendAsync(repairRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var repeatedRepairRequest = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/repair-chat-memberships", lecturerToken, new { });
        (await _client.SendAsync(repeatedRepairRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var adminRepairRequest = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/repair-chat-memberships", adminToken, new { });
        (await _client.SendAsync(adminRepairRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        context.ChangeTracker.Clear();
        var group = await context.ChatGroups.AsNoTracking()
            .SingleAsync(item => item.ClassId == seed.ClassId && item.GroupType == ChatGroupType.ClassGroup);
        (await context.ChatGroupMembers.AsNoTracking().CountAsync(item =>
            item.ChatGroupId == group.Id && item.UserId == seed.LecturerId && item.IsActive)).Should().Be(1);
        group.IsReadOnly.Should().BeFalse();

        var version = (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Version.ToString();
        using var archiveRequest = CreateAuthorizedPostRequest($"/api/classes/{seed.ClassId}/archive", lecturerToken,
            new ChangeClassLifecycleRequest { RowVersion = version, Reason = "Verify archived chat read-only state" });
        (await _client.SendAsync(archiveRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var archivedRepairRequest = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/repair-chat-memberships", lecturerToken, new { });
        (await _client.SendAsync(archivedRepairRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        context.ChangeTracker.Clear();
        (await context.ChatGroups.AsNoTracking().SingleAsync(item => item.Id == group.Id)).IsReadOnly.Should().BeTrue();
        (await context.ClassAuditLogs.AsNoTracking().CountAsync(item =>
            item.ClassId == seed.ClassId && item.Action == "CHAT_MEMBERSHIPS_REPAIRED")).Should().Be(4);
    }

    [Fact]
    public async Task AssignedLecturer_CanManageMajorLifecycleRepairAndAudit_WhileOtherLecturerCannot()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "assigned-permissions");
        var assignedLecturer = await context.Users.SingleAsync(user => user.Id == seed.LecturerId);
        var otherLecturer = await CreateLecturerAsync(context, "other-permissions");
        var assignedToken = GenerateToken(scope.ServiceProvider, assignedLecturer, SystemRoles.Lecturer);
        var otherToken = GenerateToken(scope.ServiceProvider, otherLecturer, SystemRoles.Lecturer);
        var enrollmentScope = await context.Classes.AsNoTracking()
            .Where(item => item.Id == seed.ClassId)
            .Select(item => new { item.SemesterId, item.CourseId })
            .SingleAsync();
        var studentCode = "SE" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var student = new Student
        {
            RollNumber = studentCode,
            NormalizedRollNumber = studentCode,
            FullName = "Permission Student",
            Email = $"permission-{Guid.NewGuid():N}@example.com",
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        context.Students.Add(student);
        context.ClassStudents.Add(new ClassStudent
        {
            ClassId = seed.ClassId,
            StudentId = student.Id,
            Student = student,
            SemesterId = enrollmentScope.SemesterId,
            CourseId = enrollmentScope.CourseId,
            EnrollmentStatus = EnrollmentStatus.Active,
            CountsTowardCourseSemesterLimit = true,
            MajorCodeAtEnrollment = MajorCodes.BIT_SE
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        using var forbiddenVerificationContent = new MultipartFormDataContent();
        var forbiddenVerificationFile = new ByteArrayContent(CreateImportWorkbook(studentCode, student.Email!, MajorCodes.BIT_SE));
        forbiddenVerificationFile.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        forbiddenVerificationContent.Add(forbiddenVerificationFile, "file", "major-verification.xlsx");
        using var forbiddenVerification = new HttpRequestMessage(HttpMethod.Post, $"/api/classes/{seed.ClassId}/major-verification")
        {
            Content = forbiddenVerificationContent
        };
        forbiddenVerification.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        (await _client.SendAsync(forbiddenVerification)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var allowedVerificationContent = new MultipartFormDataContent();
        var allowedVerificationFile = new ByteArrayContent(CreateImportWorkbook(studentCode, student.Email!, MajorCodes.BIT_SE));
        allowedVerificationFile.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        allowedVerificationContent.Add(allowedVerificationFile, "file", "major-verification.xlsx");
        using var allowedVerification = new HttpRequestMessage(HttpMethod.Post, $"/api/classes/{seed.ClassId}/major-verification")
        {
            Content = allowedVerificationContent
        };
        allowedVerification.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assignedToken);
        (await _client.SendAsync(allowedVerification)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var forbiddenAssignment = CreateAuthorizedPutRequest(
            $"/api/classes/{seed.ClassId}/teaching-assignment",
            assignedToken,
            new UpdateTeachingAssignmentRequest
            {
                PrimaryLecturerId = otherLecturer.Id,
                RowVersion = (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Version.ToString()
            });
        (await _client.SendAsync(forbiddenAssignment)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var forbiddenMajorCorrection = CreateAuthorizedPutRequest(
            $"/api/classes/{seed.ClassId}/students/{student.Id}/major",
            otherToken,
            new UpdateClassStudentRequest { MajorCode = MajorCodes.BIT_AI, Reason = "Attempted correction outside assigned class" });
        (await _client.SendAsync(forbiddenMajorCorrection)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var allowedMajorCorrection = CreateAuthorizedPutRequest(
            $"/api/classes/{seed.ClassId}/students/{student.Id}/major",
            assignedToken,
            new UpdateClassStudentRequest { MajorCode = MajorCodes.BIT_AI, Reason = "Correction by assigned lecturer" });
        (await _client.SendAsync(allowedMajorCorrection)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var forbiddenLock = CreateAuthorizedPostRequest($"/api/classes/{seed.ClassId}/major-lock", otherToken, new { });
        (await _client.SendAsync(forbiddenLock)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var allowedLock = CreateAuthorizedPostRequest($"/api/classes/{seed.ClassId}/major-lock", assignedToken, new { });
        (await _client.SendAsync(allowedLock)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var forbiddenAudit = new HttpRequestMessage(HttpMethod.Get, $"/api/classes/{seed.ClassId}/audit?page=1&pageSize=25");
        forbiddenAudit.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        (await _client.SendAsync(forbiddenAudit)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var allowedAudit = new HttpRequestMessage(HttpMethod.Get, $"/api/classes/{seed.ClassId}/audit?page=1&pageSize=25");
        allowedAudit.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assignedToken);
        (await _client.SendAsync(allowedAudit)).StatusCode.Should().Be(HttpStatusCode.OK);

        var activeVersion = (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Version.ToString();
        using var forbiddenArchive = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/archive",
            otherToken,
            new ChangeClassLifecycleRequest { RowVersion = activeVersion, Reason = "Attempt outside assigned class" });
        (await _client.SendAsync(forbiddenArchive)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var allowedArchive = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/archive",
            assignedToken,
            new ChangeClassLifecycleRequest { RowVersion = activeVersion, Reason = "Archive by assigned lecturer" });
        (await _client.SendAsync(allowedArchive)).StatusCode.Should().Be(HttpStatusCode.OK);

        context.ChangeTracker.Clear();
        var archivedVersion = (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Version.ToString();
        using var forbiddenRestore = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/restore",
            otherToken,
            new ChangeClassLifecycleRequest { RowVersion = archivedVersion, Reason = "Attempt restore outside assigned class" });
        (await _client.SendAsync(forbiddenRestore)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var allowedRestore = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/restore",
            assignedToken,
            new ChangeClassLifecycleRequest { RowVersion = archivedVersion, Reason = "Restore by assigned lecturer" });
        (await _client.SendAsync(allowedRestore)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CompleteAndReopenClass_TransitionsEnrollments_EnforcesReadOnly_AndKeepsStudentHistory()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "completion-roundtrip");
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var lecturer = await context.Users.SingleAsync(user => user.Id == seed.LecturerId);
        var studentRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Student);
        var email = $"completion-{Guid.NewGuid():N}@example.com";
        var studentUser = new User
        {
            FullName = "Completion Student",
            Email = email,
            NormalizedEmail = email.ToLowerInvariant(),
            PasswordHash = "integration-test-only",
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        studentUser.UserRoles.Add(new UserRole
        {
            UserId = studentUser.Id,
            User = studentUser,
            RoleId = studentRole.Id,
            Role = studentRole
        });
        var rollNumber = $"SE{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var student = new Student
        {
            UserId = studentUser.Id,
            User = studentUser,
            RollNumber = rollNumber,
            NormalizedRollNumber = rollNumber,
            FullName = studentUser.FullName,
            Email = email,
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            CreatedBy = seed.AdminId
        };
        var targetClass = await context.Classes.SingleAsync(item => item.Id == seed.ClassId);
        context.Students.Add(student);
        context.ClassStudents.Add(new ClassStudent
        {
            ClassId = targetClass.Id,
            Class = targetClass,
            StudentId = student.Id,
            Student = student,
            SemesterId = targetClass.SemesterId,
            CourseId = targetClass.CourseId,
            EnrollmentStatus = EnrollmentStatus.Active,
            CountsTowardCourseSemesterLimit = true,
            MajorCodeAtEnrollment = MajorCodes.BIT_SE
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var lecturerToken = GenerateToken(scope.ServiceProvider, lecturer, SystemRoles.Lecturer);
        var adminToken = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);
        var studentToken = GenerateToken(scope.ServiceProvider, studentUser, SystemRoles.Student);
        using (var repair = CreateAuthorizedPostRequest(
                   $"/api/classes/{seed.ClassId}/repair-chat-memberships", lecturerToken, new { }))
            (await _client.SendAsync(repair)).StatusCode.Should().Be(HttpStatusCode.OK);

        var version = (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Version.ToString();
        using var complete = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/complete",
            lecturerToken,
            new ChangeClassLifecycleRequest { RowVersion = version, Reason = "Academic work has been finalized" });
        (await _client.SendAsync(complete)).StatusCode.Should().Be(HttpStatusCode.OK);

        await scope.ServiceProvider.GetRequiredService<IClassChatMembershipSynchronizer>()
            .SynchronizeAsync(seed.ClassId, seed.LecturerId);

        context.ChangeTracker.Clear();
        var completedClass = await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId);
        var completedEnrollment = await context.ClassStudents.AsNoTracking()
            .SingleAsync(item => item.ClassId == seed.ClassId && item.StudentId == student.Id);
        completedClass.Status.Should().Be(ClassStatus.Completed);
        completedClass.CompletedAtUtc.Should().NotBeNull();
        completedEnrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Completed);
        completedEnrollment.CompletedAtUtc.Should().NotBeNull();
        completedEnrollment.CountsTowardCourseSemesterLimit.Should().BeTrue();
        (await context.ChatGroups.AsNoTracking().SingleAsync(item =>
            item.ClassId == seed.ClassId && item.GroupType == ChatGroupType.ClassGroup)).IsReadOnly.Should().BeTrue();
        (await context.ChatGroupMembers.AsNoTracking().SingleAsync(item =>
            item.ChatGroup.ClassId == seed.ClassId &&
            item.ChatGroup.GroupType == ChatGroupType.ClassGroup &&
            item.StudentId == student.Id)).IsActive.Should().BeTrue();
        (await context.ClassAuditLogs.CountAsync(item => item.ClassId == seed.ClassId && item.Action == "CLASS_COMPLETED"))
            .Should().Be(1);
        (await context.OutboxMessages.CountAsync(item => item.AggregateId == seed.ClassId && item.Type == "Class.Completed.v1"))
            .Should().Be(1);

        using var forbiddenMutation = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/students",
            lecturerToken,
            new AddStudentToClassRequest
            {
                StudentCode = $"SE{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
                FullName = "Read Only Student",
                Email = $"readonly-{Guid.NewGuid():N}@example.com",
                MajorCode = MajorCodes.BIT_SE
            });
        var mutationResponse = await _client.SendAsync(forbiddenMutation);
        mutationResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await mutationResponse.Content.ReadFromJsonAsync<ApiResponse<object>>())!.Code.Should().Be(ErrorCodes.ClassCompleted);

        using var history = new HttpRequestMessage(HttpMethod.Get, "/api/classes/my-classes?scope=History");
        history.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var historyResponse = await _client.SendAsync(history);
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await historyResponse.Content.ReadAsStringAsync()).Should().Contain(targetClass.ClassCode);

        using var archiveCompleted = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/archive",
            lecturerToken,
            new ChangeClassLifecycleRequest { RowVersion = completedClass.Version.ToString(), Reason = "Preserve completed history" });
        (await _client.SendAsync(archiveCompleted)).StatusCode.Should().Be(HttpStatusCode.OK);
        context.ChangeTracker.Clear();
        var archivedCompleted = await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId);
        archivedCompleted.Status.Should().Be(ClassStatus.Archived);
        archivedCompleted.StatusBeforeArchive.Should().Be(ClassStatus.Completed);

        using (var archivedDetail = new HttpRequestMessage(HttpMethod.Get, $"/api/classes/{seed.ClassId}"))
        {
            archivedDetail.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var archivedDetailResponse = await _client.SendAsync(archivedDetail);
            archivedDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var archivedDetailBody = await archivedDetailResponse.Content.ReadFromJsonAsync<ApiResponse<ClassResponse>>();
            archivedDetailBody!.Data!.StudentCount.Should().Be(1);
            archivedDetailBody.Data.StatusBeforeArchive.Should().Be(nameof(ClassStatus.Completed));
        }

        using var restoreCompleted = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/restore",
            lecturerToken,
            new ChangeClassLifecycleRequest { RowVersion = archivedCompleted.Version.ToString(), Reason = "Restore completed history" });
        (await _client.SendAsync(restoreCompleted)).StatusCode.Should().Be(HttpStatusCode.OK);
        context.ChangeTracker.Clear();
        completedClass = await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId);
        completedClass.Status.Should().Be(ClassStatus.Completed);

        using var lecturerReopen = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/reopen",
            lecturerToken,
            new ChangeClassLifecycleRequest { RowVersion = completedClass.Version.ToString(), Reason = "Lecturer cannot reopen" });
        (await _client.SendAsync(lecturerReopen)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var adminReopen = CreateAuthorizedPostRequest(
            $"/api/classes/{seed.ClassId}/reopen",
            adminToken,
            new ChangeClassLifecycleRequest { RowVersion = completedClass.Version.ToString(), Reason = "Correction window approved" });
        (await _client.SendAsync(adminReopen)).StatusCode.Should().Be(HttpStatusCode.OK);

        context.ChangeTracker.Clear();
        (await context.Classes.AsNoTracking().SingleAsync(item => item.Id == seed.ClassId)).Status.Should().Be(ClassStatus.Active);
        var reopenedEnrollment = await context.ClassStudents.AsNoTracking()
            .SingleAsync(item => item.ClassId == seed.ClassId && item.StudentId == student.Id);
        reopenedEnrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
        reopenedEnrollment.CompletedAtUtc.Should().BeNull();
        (await context.ChatGroups.AsNoTracking().SingleAsync(item =>
            item.ClassId == seed.ClassId && item.GroupType == ChatGroupType.ClassGroup)).IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public async Task BulkCreate_AssignsDifferentLecturersToExplicitClassIndices()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "bulk-multi-assignment");
        var secondLecturer = await CreateLecturerAsync(context, "bulk-multi-assignment-second");
        var sourceClass = await context.Classes.SingleAsync(item => item.Id == seed.ClassId);
        var semester = await context.Semesters.SingleAsync(item => item.Id == sourceClass.SemesterId);
        semester.Status = SemesterStatus.Active;
        semester.StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        semester.EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await context.SaveChangesAsync();

        var firstIndex = await context.Classes
            .Where(item => item.CourseId == sourceClass.CourseId && item.SemesterId == sourceClass.SemesterId)
            .MaxAsync(item => item.ClassIndex) + 1;
        var indices = new[] { firstIndex, firstIndex + 1, firstIndex + 2 };
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var token = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);

        using var request = CreateAuthorizedPostRequest(
            "/api/classes/bulk/commit",
            token,
            new CreateBulkClassesRequest
            {
                CourseId = sourceClass.CourseId,
                SemesterId = sourceClass.SemesterId,
                ClassIndices = indices,
                LecturerAssignments =
                [
                    new BulkClassLecturerAssignmentRequest
                    {
                        LecturerId = seed.LecturerId,
                        ClassIndices = [indices[0], indices[2]]
                    },
                    new BulkClassLecturerAssignmentRequest
                    {
                        LecturerId = secondLecturer.Id,
                        ClassIndices = [indices[1]]
                    }
                ]
            });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ClassResponse[]>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Data.Should().HaveCount(3);
        body.Data!.Single(item => item.ClassIndex == indices[0]).PrimaryLecturerId.Should().Be(seed.LecturerId);
        body.Data.Single(item => item.ClassIndex == indices[1]).PrimaryLecturerId.Should().Be(secondLecturer.Id);
        body.Data.Single(item => item.ClassIndex == indices[2]).PrimaryLecturerId.Should().Be(seed.LecturerId);

        context.ChangeTracker.Clear();
        var created = await context.Classes.AsNoTracking()
            .Where(item => item.CourseId == sourceClass.CourseId && indices.Contains(item.ClassIndex))
            .ToListAsync();
        created.Should().HaveCount(3);
        created.Single(item => item.ClassIndex == indices[1]).PrimaryLecturerId.Should().Be(secondLecturer.Id);
        (await context.ClassLecturers.AsNoTracking()
            .CountAsync(item => created.Select(createdClass => createdClass.Id).Contains(item.ClassId) && item.IsPrimary))
            .Should().Be(3);
    }

    [Fact]
    public async Task BulkPreview_WhenAssignmentsOverlap_Returns400WithoutCreatingClasses()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateClassSeedAsync(context, "bulk-overlap");
        var secondLecturer = await CreateLecturerAsync(context, "bulk-overlap-second");
        var sourceClass = await context.Classes.SingleAsync(item => item.Id == seed.ClassId);
        var semester = await context.Semesters.SingleAsync(item => item.Id == sourceClass.SemesterId);
        semester.Status = SemesterStatus.Active;
        semester.StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        semester.EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await context.SaveChangesAsync();
        var classIndex = await context.Classes
            .Where(item => item.CourseId == sourceClass.CourseId && item.SemesterId == sourceClass.SemesterId)
            .MaxAsync(item => item.ClassIndex) + 1;
        var admin = await context.Users.SingleAsync(user => user.Id == seed.AdminId);
        var token = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);

        using var request = CreateAuthorizedPostRequest(
            "/api/classes/bulk/preview",
            token,
            new CreateBulkClassesRequest
            {
                CourseId = sourceClass.CourseId,
                SemesterId = sourceClass.SemesterId,
                ClassIndices = [classIndex],
                LecturerAssignments =
                [
                    new BulkClassLecturerAssignmentRequest { LecturerId = seed.LecturerId, ClassIndices = [classIndex] },
                    new BulkClassLecturerAssignmentRequest { LecturerId = secondLecturer.Id, ClassIndices = [classIndex] }
                ]
            });

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body!.Code.Should().Be(ErrorCodes.ClassValidationError);
        body.Message.Should().Contain("more than one lecturer");
        (await context.Classes.AsNoTracking().AnyAsync(item =>
            item.CourseId == sourceClass.CourseId &&
            item.SemesterId == sourceClass.SemesterId &&
            item.ClassIndex == classIndex)).Should().BeFalse();
    }

    [Fact]
    public async Task AdminCanPlanAndCorrectSemesterDates_WithAuditAndConcurrency()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await context.Users
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstAsync(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin));
        var token = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);
        var year = DateTime.UtcNow.Year + 2;
        var startDate = new DateOnly(year, 1, 5);
        var endDate = new DateOnly(year, 4, 20);

        using var planRequest = CreateAuthorizedPostRequest(
            "/api/subjects/semesters",
            token,
            new PlanSemesterRequest
            {
                Semester = "SP",
                Year = year,
                StartDate = startDate,
                EndDate = endDate
            });
        var planResponse = await _client.SendAsync(planRequest);
        var plannedBody = await planResponse.Content.ReadFromJsonAsync<ApiResponse<SemesterResponse>>();

        planResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        plannedBody!.Data!.Status.Should().Be(nameof(SemesterStatus.Planned));
        plannedBody.Data.StartDate.Should().Be(startDate);

        var correctedStart = startDate.AddDays(2);
        var correctedEnd = endDate.AddDays(2);
        using var updateRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/subjects/semesters/{plannedBody.Data.Id}/dates")
        {
            Content = JsonContent.Create(new UpdateSemesterDatesRequest
            {
                StartDate = correctedStart,
                EndDate = correctedEnd,
                RowVersion = plannedBody.Data.RowVersion,
                Reason = "Academic calendar correction"
            })
        };
        updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateResponse = await _client.SendAsync(updateRequest);
        var updatedBody = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<SemesterResponse>>();

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedBody!.Data!.StartDate.Should().Be(correctedStart);
        updatedBody.Data.EndDate.Should().Be(correctedEnd);
        updatedBody.Data.RowVersion.Should().NotBe(plannedBody.Data.RowVersion);

        context.ChangeTracker.Clear();
        var auditActions = await context.SemesterAuditLogs.AsNoTracking()
            .Where(item => item.SemesterId == plannedBody.Data.Id)
            .Select(item => item.Action)
            .ToListAsync();
        auditActions.Should().Contain("SEMESTER_PLANNED");
        auditActions.Should().Contain("SEMESTER_DATES_UPDATED");
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
        var semester = await context.Semesters.FirstAsync(item => item.Status == SemesterStatus.Active);
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

    private static async Task<Guid> CreateSiblingClassAsync(AppDbContext context, ClassSeed seed)
    {
        var targetClass = await context.Classes.AsNoTracking()
            .SingleAsync(@class => @class.Id == seed.ClassId);
        var sibling = new Class
        {
            ClassCode = $"{targetClass.ClassCode}_S{Guid.NewGuid():N}"[..Math.Min(50, targetClass.ClassCode.Length + 10)],
            ClassIndex = targetClass.ClassIndex + 100,
            CourseId = targetClass.CourseId,
            SemesterId = targetClass.SemesterId,
            PrimaryLecturerId = seed.LecturerId,
            ScheduleJson = "[{\"dayOfWeek\":5,\"slotNumber\":6,\"room\":\"SH-506\"}]",
            Status = ClassStatus.Active,
            CreatedById = seed.AdminId,
            CreatedBy = seed.AdminId
        };
        context.Classes.Add(sibling);
        context.ClassLecturers.Add(new ClassLecturer
        {
            ClassId = sibling.Id,
            LecturerId = seed.LecturerId,
            IsPrimary = true,
            AssignedById = seed.AdminId
        });
        await context.SaveChangesAsync();
        return sibling.Id;
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

    private static HttpRequestMessage CreateAuthorizedPostRequest(string url, string token, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static byte[] CreateImportWorkbook(string studentCode, string email, string majorCode)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Students");
        worksheet.Cell(1, 1).Value = "StudentCode";
        worksheet.Cell(1, 2).Value = "FullName";
        worksheet.Cell(1, 3).Value = "Email";
        worksheet.Cell(1, 4).Value = "MajorCode";
        worksheet.Cell(2, 1).Value = studentCode;
        worksheet.Cell(2, 2).Value = "Imported Student";
        worksheet.Cell(2, 3).Value = email;
        worksheet.Cell(2, 4).Value = majorCode;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record ClassSeed(Guid ClassId, Guid AdminId, Guid LecturerId, string ScheduleJson);

    private sealed class ThrowWhenEnrollmentIsInsertedInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<ClassStudent>()
                .Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new DbUpdateException("Simulated system failure while persisting an enrollment.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
