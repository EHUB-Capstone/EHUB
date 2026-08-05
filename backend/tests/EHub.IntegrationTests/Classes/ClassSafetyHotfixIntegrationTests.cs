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
using EHub.Application.Features.Classes.AddStudentToClass;
using EHub.Application.Features.Classes.CreateClass;
using EHub.Application.Features.Classes.ImportStudents;
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
    public async Task LecturerCreatedClass_StaysDraftUntilScheduleMakesItActive()
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
                ClassIndex = nextIndex
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

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
    public async Task AddingExistingStudent_StoresEnrollmentMajorSnapshotWithoutChangingProfileMajor()
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
                MajorCode = MajorCodes.BIT_SE
            },
            seed.LecturerId,
            SystemRoles.Lecturer);

        result.IsSuccess.Should().BeTrue();
        result.Value.MajorCode.Should().Be(MajorCodes.BIT_SE);
        result.Value.ProfileMajorCode.Should().Be(MajorCodes.BIT_AI);
        context.ChangeTracker.Clear();

        var persistedProfile = await context.Students.AsNoTracking().SingleAsync(item => item.Id == student.Id);
        var enrollment = await context.ClassStudents.AsNoTracking()
            .SingleAsync(item => item.ClassId == seed.ClassId && item.StudentId == student.Id);
        persistedProfile.FullName.Should().Be("Profile Source Of Truth");
        persistedProfile.MajorCode.Should().Be(MajorCodes.BIT_AI);
        enrollment.MajorCodeAtEnrollment.Should().Be(MajorCodes.BIT_SE);
        enrollment.MajorVerificationStatus.Should().Be(EnrollmentMajorVerificationStatus.Unverified);
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
            ScheduleJson = "[]",
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
