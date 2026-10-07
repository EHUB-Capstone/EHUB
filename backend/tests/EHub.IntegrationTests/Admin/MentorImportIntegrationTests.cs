using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
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
    public async Task AdminImport_NameOnlyRows_ShouldSaveDrafts_ThenCompleteThemFromFullWorkbook()
    {
        var token = await GetAdminTokenAsync();
        var semesterId = await GetSemesterIdAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var enterpriseName = $"Incomplete Enterprise {suffix}";
        var academicName = $"Incomplete Academic {suffix}";
        var enterpriseEmail = $"incomplete-enterprise-{suffix}@example.com";
        var academicEmail = $"incomplete-academic-{suffix}@example.edu.vn";

        using var draftPreviewRequest = CreatePreviewRequest(semesterId, CreateNameOnlyMentorWorkbook(enterpriseName, academicName), token);
        var draftPreviewResponse = await _client.SendAsync(draftPreviewRequest);
        var draftPreview = await draftPreviewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();

        draftPreviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        draftPreview!.Data!.CanCommit.Should().BeTrue();
        draftPreview.Data.NeedsCompletionCount.Should().Be(2);
        draftPreview.Data.ErrorCount.Should().Be(0);

        using var draftCommitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/imports/commit")
        {
            Content = JsonContent.Create(new CommitMentorImportRequest { SessionId = draftPreview.Data.SessionId })
        };
        draftCommitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var draftCommitResponse = await _client.SendAsync(draftCommitRequest);
        var draftCommit = await draftCommitResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportCommitResponse>>();

        draftCommitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        draftCommit!.Data!.DraftSavedCount.Should().Be(2);
        draftCommit.Data.CreatedCount.Should().Be(0);

        string semesterCode;
        int semesterYear;
        using (var verifyDraftScope = factory.Services.CreateScope())
        {
            var context = verifyDraftScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var semester = await context.Semesters.AsNoTracking().SingleAsync(item => item.Id == semesterId);
            semesterCode = semester.Term switch
            {
                SemesterTerm.Spring => "SP",
                SemesterTerm.Summer => "SU",
                SemesterTerm.Fall => "FA",
                _ => throw new InvalidOperationException("Unsupported semester term.")
            };
            semesterYear = semester.Year;
            var drafts = await context.MentorImportDrafts.AsNoTracking()
                .Where(item => item.SemesterId == semesterId && (item.FullName == enterpriseName || item.FullName == academicName))
                .ToListAsync();
            drafts.Should().HaveCount(2);
            drafts.Should().OnlyContain(item => item.Status == MentorImportDraftStatus.NeedsCompletion);
        }

        using var draftListRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/subjects/teaching-staff?semester={semesterCode}&year={semesterYear}");
        draftListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var draftListResponse = await _client.SendAsync(draftListRequest);
        var draftList = await draftListResponse.Content.ReadFromJsonAsync<ApiResponse<TeachingStaffListResponse>>();

        draftListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        draftList!.Data!.Staff.Should().Contain(item =>
            item.Name == enterpriseName && item.IsIncomplete && item.UserId == null && item.Status == "Incomplete");
        draftList.Data.Staff.Should().Contain(item =>
            item.Name == academicName && item.IsIncomplete && item.UserId == null && item.Status == "Incomplete");

        using var completePreviewRequest = CreatePreviewRequest(semesterId,
            CreateMentorWorkbook(enterpriseEmail, academicEmail, enterpriseName, academicName), token);
        var completePreviewResponse = await _client.SendAsync(completePreviewRequest);
        var completePreview = await completePreviewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();

        completePreviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        completePreview!.Data!.CanCommit.Should().BeTrue();
        completePreview.Data.CompleteDraftCount.Should().Be(2);

        using var completeCommitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/imports/commit")
        {
            Content = JsonContent.Create(new CommitMentorImportRequest { SessionId = completePreview.Data.SessionId })
        };
        completeCommitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var completeCommitResponse = await _client.SendAsync(completeCommitRequest);
        var completeCommit = await completeCommitResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportCommitResponse>>();

        completeCommitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        completeCommit!.Data!.CreatedCount.Should().Be(2);
        completeCommit.Data.DraftCompletedCount.Should().Be(2);

        using var completedListRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/subjects/teaching-staff?semester={semesterCode}&year={semesterYear}");
        completedListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var completedListResponse = await _client.SendAsync(completedListRequest);
        var completedList = await completedListResponse.Content.ReadFromJsonAsync<ApiResponse<TeachingStaffListResponse>>();

        completedListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        completedList!.Data!.Staff.Should().Contain(item =>
            item.Name == enterpriseName && !item.IsIncomplete && item.Email == enterpriseEmail);
        completedList.Data.Staff.Should().Contain(item =>
            item.Name == academicName && !item.IsIncomplete && item.Email == academicEmail);

        using var minimalPreviewRequest = CreatePreviewRequest(semesterId,
            CreateIdentityOnlyMentorWorkbook(enterpriseEmail, academicEmail, enterpriseName, academicName), token);
        var minimalPreviewResponse = await _client.SendAsync(minimalPreviewRequest);
        var minimalPreview = await minimalPreviewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();
        minimalPreviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        minimalPreview!.Data!.CanCommit.Should().BeTrue();
        using var minimalCommitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/imports/commit")
        {
            Content = JsonContent.Create(new CommitMentorImportRequest { SessionId = minimalPreview.Data.SessionId })
        };
        minimalCommitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await _client.SendAsync(minimalCommitRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var completedDrafts = await verifyContext.MentorImportDrafts.AsNoTracking()
            .Where(item => item.SemesterId == semesterId && (item.FullName == enterpriseName || item.FullName == academicName))
            .ToListAsync();
        completedDrafts.Should().OnlyContain(item => item.Status == MentorImportDraftStatus.Converted && item.ConvertedMentorProfileId != null);
        var profiles = await verifyContext.MentorProfiles.AsNoTracking().Include(item => item.User)
            .Where(item => item.User.NormalizedEmail == enterpriseEmail || item.User.NormalizedEmail == academicEmail)
            .ToListAsync();
        profiles.Should().HaveCount(2);
        profiles.Single(item => item.Type == MentorType.Enterprise).Organization.Should().Be("Integration Company");
        profiles.Single(item => item.Type == MentorType.Academic).Department.Should().Be("Bộ môn CNTT");
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

    [Fact]
    public async Task Allocation_ShouldRetainMentorsForContinuingTeamsAndReportTheOnesThatCannotContinue()
    {
        var token = await GetAdminTokenAsync();
        var completedAt = DateTime.UtcNow.AddDays(-5);
        Guid targetSemesterId, targetClassId, continuingTeamId, newTeamId;
        Guid retainedEnterpriseId, retainedAcademicId, droppedEnterpriseId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminId = await context.Users.Where(item => item.NormalizedEmail == "admin@ehub.test").Select(item => item.Id).SingleAsync();
            var mentorRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Mentor);
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var exe101 = await context.Courses.SingleOrDefaultAsync(item => item.Code == "EXE101")
                         ?? new Course { Code = "EXE101", Name = "EXE101", Status = CourseStatus.Active };
            var exe201 = await context.Courses.SingleOrDefaultAsync(item => item.Code == "EXE201")
                         ?? new Course { Code = "EXE201", Name = "EXE201", Status = CourseStatus.Active };
            if (context.Entry(exe101).State == EntityState.Detached) context.Courses.Add(exe101);
            if (context.Entry(exe201).State == EntityState.Detached) context.Courses.Add(exe201);

            var previousSemester = new Semester { Code = $"RP{suffix}", Name = $"Retention prev {suffix}", Term = SemesterTerm.Fall, Year = 2070, Status = SemesterStatus.Planned };
            var targetSemester = new Semester { Code = $"RT{suffix}", Name = $"Retention target {suffix}", Term = SemesterTerm.Spring, Year = 2071, Status = SemesterStatus.Planned };
            context.Semesters.AddRange(previousSemester, targetSemester);

            const string schedule = "[{\"dayOfWeek\":1,\"startTime\":\"08:00\",\"endTime\":\"10:00\"}]";
            var previousClass = new Class { SemesterId = previousSemester.Id, Semester = previousSemester, CourseId = exe101.Id, Course = exe101, ClassCode = $"P101{suffix}", Slug = $"p101-{suffix}", ClassIndex = 1, Status = ClassStatus.Completed, CompletedAtUtc = completedAt, CompletionReason = "Integration test", PrimaryLecturerId = adminId, ScheduleJson = schedule, CreatedById = adminId };
            var targetClass = new Class { SemesterId = targetSemester.Id, Semester = targetSemester, CourseId = exe201.Id, Course = exe201, ClassCode = $"T201{suffix}", Slug = $"t201-{suffix}", ClassIndex = 1, Status = ClassStatus.Active, PrimaryLecturerId = adminId, ScheduleJson = schedule, CreatedById = adminId };
            context.Classes.AddRange(previousClass, targetClass);

            Team NewTeam(Class owner, string code, Team? previous = null) => new()
            {
                ClassId = owner.Id, Class = owner, TeamCode = code, TeamName = code, Status = TeamStatus.Active, CreatedById = adminId,
                PreviousTeamId = previous?.Id, TeamLineageId = previous?.TeamLineageId ?? Guid.NewGuid()
            };
            var continuingSource = NewTeam(previousClass, $"P101{suffix}_G1");
            var droppedSource = NewTeam(previousClass, $"P101{suffix}_G2");
            var continuing = NewTeam(targetClass, $"T201{suffix}_G1", continuingSource);
            var fresh = NewTeam(targetClass, $"T201{suffix}_G2");
            context.Teams.AddRange(continuingSource, droppedSource, continuing, fresh);

            MentorProfile NewMentor(MentorType type, string label, bool activeInTarget)
            {
                var email = $"retention-{label}-{suffix}@example.com";
                var user = new User { FullName = $"Retention {label}", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
                var profile = new MentorProfile { UserId = user.Id, User = user, Type = type, Status = MentorProfileStatus.Active, CreatedBy = adminId };
                context.Users.Add(user);
                context.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = mentorRole.Id, Role = mentorRole, AssignedAt = DateTime.UtcNow, AssignedBy = adminId });
                context.MentorProfiles.Add(profile);
                if (activeInTarget)
                    context.SemesterStaffAssignments.Add(new SemesterStaffAssignment { SemesterId = targetSemester.Id, Semester = targetSemester, UserId = user.Id, User = user, Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active, CreatedBy = adminId });
                return profile;
            }
            var retainedEnterprise = NewMentor(MentorType.Enterprise, "ent-kept", true);
            var retainedAcademic = NewMentor(MentorType.Academic, "acad-kept", true);
            var droppedEnterprise = NewMentor(MentorType.Enterprise, "ent-dropped", true);
            NewMentor(MentorType.Enterprise, "ent-spare", true);
            NewMentor(MentorType.Academic, "acad-spare", true);

            MentorAssignment EndedAtCompletion(Team team, MentorProfile mentor) => new()
            {
                TeamId = team.Id, Team = team, MentorProfileId = mentor.Id, MentorProfile = mentor, AssignedById = adminId,
                AssignedAt = completedAt.AddDays(-60), EndedAt = completedAt, Slot = mentor.Type, Status = MentorAssignmentStatus.Ended, CreatedBy = adminId
            };
            context.MentorAssignments.AddRange(
                EndedAtCompletion(continuingSource, retainedEnterprise),
                EndedAtCompletion(continuingSource, retainedAcademic),
                EndedAtCompletion(droppedSource, droppedEnterprise));
            await context.SaveChangesAsync();

            targetSemesterId = targetSemester.Id;
            targetClassId = targetClass.Id;
            continuingTeamId = continuing.Id;
            newTeamId = fresh.Id;
            retainedEnterpriseId = retainedEnterprise.Id;
            retainedAcademicId = retainedAcademic.Id;
            droppedEnterpriseId = droppedEnterprise.Id;
        }

        using var previewRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/preview")
        {
            Content = JsonContent.Create(new PreviewMentorAllocationRequest { SemesterId = targetSemesterId, ClassIds = [targetClassId], Seed = 7 })
        };
        previewRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var previewResponse = await _client.SendAsync(previewRequest);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>();

        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        preview!.Data!.CanCommit.Should().BeTrue();
        preview.Data.RetainedCount.Should().Be(2);
        preview.Data.Assignments.Should().HaveCount(4);

        var continuingRows = preview.Data.Assignments.Where(item => item.TeamId == continuingTeamId).ToArray();
        continuingRows.Should().OnlyContain(item => item.Source == MentorAllocationSources.Retained);
        continuingRows.Single(item => item.MentorType == nameof(MentorType.Enterprise)).MentorProfileId.Should().Be(retainedEnterpriseId);
        continuingRows.Single(item => item.MentorType == nameof(MentorType.Academic)).MentorProfileId.Should().Be(retainedAcademicId);

        var freshRows = preview.Data.Assignments.Where(item => item.TeamId == newTeamId).ToArray();
        freshRows.Should().HaveCount(2).And.OnlyContain(item => item.Source == MentorAllocationSources.Allocated);
        freshRows.Should().NotContain(item => item.MentorProfileId == retainedEnterpriseId, "the retained mentor already carries a team and the spare mentor has none");

        var skipped = preview.Data.Skipped.Should().ContainSingle().Subject;
        skipped.Reason.Should().Be("NoContinuedTeam");
        skipped.MentorProfileId.Should().Be(droppedEnterpriseId);

        using var commitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/commit")
        {
            Content = JsonContent.Create(new CommitMentorAllocationRequest { SessionId = preview.Data.SessionId })
        };
        commitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await _client.SendAsync(commitRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var active = await verifyContext.MentorAssignments.AsNoTracking()
            .Where(item => (item.TeamId == continuingTeamId || item.TeamId == newTeamId) && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
            .ToListAsync();
        active.Should().HaveCount(4);
        active.Where(item => item.TeamId == continuingTeamId).Select(item => item.MentorProfileId)
            .Should().BeEquivalentTo([retainedEnterpriseId, retainedAcademicId]);
        active.Where(item => item.TeamId == continuingTeamId).Should().OnlyContain(item => item.Note != null && item.Note.StartsWith("Retained"));
    }

    [Fact]
    public async Task RandomAllocation_ShouldSaveWhatCanBeAssignedAndReportTheMissingMentorType()
    {
        var token = await GetAdminTokenAsync();
        Guid semesterId, classId;
        Guid[] teamIds;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminId = await context.Users.Where(item => item.NormalizedEmail == "admin@ehub.test").Select(item => item.Id).SingleAsync();
            var mentorRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Mentor);
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var semester = new Semester { Code = $"RN{suffix}", Name = $"Random {suffix}", Term = SemesterTerm.Summer, Year = 2060, Status = SemesterStatus.Planned };
            var course = new Course { Code = $"R{suffix}", Name = "Random Course", Status = CourseStatus.Active };
            var targetClass = new Class { SemesterId = semester.Id, Semester = semester, CourseId = course.Id, Course = course, ClassCode = $"RC{suffix}", Slug = $"rc-{suffix}", ClassIndex = 1, Status = ClassStatus.Active, PrimaryLecturerId = adminId, ScheduleJson = "[{\"dayOfWeek\":1,\"startTime\":\"08:00\",\"endTime\":\"10:00\"}]", CreatedById = adminId };
            context.Semesters.Add(semester);
            context.Courses.Add(course);
            context.Classes.Add(targetClass);
            var teams = Enumerable.Range(1, 3).Select(index => new Team
            {
                ClassId = targetClass.Id, Class = targetClass, TeamCode = $"{targetClass.ClassCode}_T{index}", TeamName = $"Team {index}", Status = TeamStatus.Active, CreatedById = adminId
            }).ToArray();
            context.Teams.AddRange(teams);
            for (var index = 1; index <= 2; index++)
            {
                var email = $"random-enterprise-{index}-{suffix}@example.com";
                var user = new User { FullName = $"Random Enterprise {index}", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
                context.Users.Add(user);
                context.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = mentorRole.Id, Role = mentorRole, AssignedAt = DateTime.UtcNow, AssignedBy = adminId });
                context.MentorProfiles.Add(new MentorProfile { UserId = user.Id, User = user, Type = MentorType.Enterprise, ContractType = "Thỉnh giảng", Status = MentorProfileStatus.Active, CreatedBy = adminId });
                context.SemesterStaffAssignments.Add(new SemesterStaffAssignment { SemesterId = semester.Id, Semester = semester, UserId = user.Id, User = user, Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active, CreatedBy = adminId });
            }
            await context.SaveChangesAsync();
            semesterId = semester.Id;
            classId = targetClass.Id;
            teamIds = teams.Select(item => item.Id).ToArray();
        }

        async Task<HttpResponseMessage> PreviewAsync(string strategy)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/preview")
            {
                Content = JsonContent.Create(new PreviewMentorAllocationRequest { SemesterId = semesterId, ClassIds = [classId], Seed = 3, Strategy = strategy })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await _client.SendAsync(request);
        }

        (await PreviewAsync("Nonsense")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var previewResponse = await PreviewAsync(MentorAllocationStrategies.Random);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>();
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = preview!.Data!;
        data.Strategy.Should().Be(MentorAllocationStrategies.Random);
        data.CanCommit.Should().BeTrue("a missing mentor type must not block saving what can be assigned");
        data.Assignments.Should().HaveCount(3).And.OnlyContain(item => item.MentorType == nameof(MentorType.Enterprise));
        data.Warnings.Should().ContainSingle().Which.Should().Contain("Academic");
        data.UnfilledAcademicCount.Should().Be(3);
        data.UnfilledEnterpriseCount.Should().Be(0);
        data.Unfilled.Should().HaveCount(3).And.OnlyContain(item => item.MentorType == nameof(MentorType.Academic) && item.SubjectCode.StartsWith("R"));
        data.MentorLoads.Should().HaveCount(2);
        data.MentorLoads.Should().OnlyContain(item => item.ContractType == "Thỉnh giảng" && item.TotalBefore == 0);
        data.MentorLoads.Sum(item => item.TotalAfter).Should().Be(3);

        using var commitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/commit")
        {
            Content = JsonContent.Create(new CommitMentorAllocationRequest { SessionId = data.SessionId })
        };
        commitRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await _client.SendAsync(commitRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = factory.Services.CreateScope();
        var saved = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking()
            .Where(item => teamIds.Contains(item.TeamId) && item.Status == MentorAssignmentStatus.Active).ToListAsync();
        saved.Should().HaveCount(3).And.OnlyContain(item => item.Slot == MentorType.Enterprise);
    }

    private sealed record ReplaceFixture(
        Guid SemesterId, Guid ClassId, Guid Team1, Guid Team2,
        Guid CurrentAssignmentId, Guid CurrentMentor, Guid NewMentor, Guid AcademicMentor);

    // Team 1 already has an active Enterprise mentor ("current"). "New" and one Academic mentor are free to be assigned.
    private async Task<ReplaceFixture> SeedReplaceFixtureAsync(int year)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminId = await context.Users.Where(item => item.NormalizedEmail == "admin@ehub.test").Select(item => item.Id).SingleAsync();
        var mentorRole = await context.Roles.SingleAsync(item => item.Name == SystemRoles.Mentor);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var semester = new Semester { Code = $"RE{suffix}", Name = $"Replace {suffix}", Term = SemesterTerm.Summer, Year = year, Status = SemesterStatus.Planned };
        var course = new Course { Code = $"P{suffix}", Name = "Replace Course", Status = CourseStatus.Active };
        var targetClass = new Class { SemesterId = semester.Id, Semester = semester, CourseId = course.Id, Course = course, ClassCode = $"RP{suffix}", Slug = $"rp-{suffix}", ClassIndex = 1, Status = ClassStatus.Active, PrimaryLecturerId = adminId, ScheduleJson = "[{\"dayOfWeek\":1,\"startTime\":\"08:00\",\"endTime\":\"10:00\"}]", CreatedById = adminId };
        context.Semesters.Add(semester);
        context.Courses.Add(course);
        context.Classes.Add(targetClass);
        var team1 = new Team { ClassId = targetClass.Id, Class = targetClass, TeamCode = $"{targetClass.ClassCode}_T1", TeamName = "Team 1", Status = TeamStatus.Active, CreatedById = adminId };
        var team2 = new Team { ClassId = targetClass.Id, Class = targetClass, TeamCode = $"{targetClass.ClassCode}_T2", TeamName = "Team 2", Status = TeamStatus.Active, CreatedById = adminId };
        context.Teams.AddRange(team1, team2);

        MentorProfile NewMentor(MentorType type, string label)
        {
            var email = $"replace-{label}-{suffix}@example.com";
            var user = new User { FullName = $"Replace {label}", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
            var profile = new MentorProfile { UserId = user.Id, User = user, Type = type, Status = MentorProfileStatus.Active, CreatedBy = adminId };
            context.Users.Add(user);
            context.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = mentorRole.Id, Role = mentorRole, AssignedAt = DateTime.UtcNow, AssignedBy = adminId });
            context.MentorProfiles.Add(profile);
            context.SemesterStaffAssignments.Add(new SemesterStaffAssignment { SemesterId = semester.Id, Semester = semester, UserId = user.Id, User = user, Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active, CreatedBy = adminId });
            return profile;
        }
        var current = NewMentor(MentorType.Enterprise, "current");
        var replacement = NewMentor(MentorType.Enterprise, "new");
        var academic = NewMentor(MentorType.Academic, "academic");
        var assignment = new MentorAssignment
        {
            TeamId = team1.Id, Team = team1, MentorProfileId = current.Id, MentorProfile = current, AssignedById = adminId,
            AssignedAt = DateTime.UtcNow.AddDays(-1), Slot = MentorType.Enterprise, Status = MentorAssignmentStatus.Active, CreatedBy = adminId
        };
        context.MentorAssignments.Add(assignment);
        await context.SaveChangesAsync();
        return new ReplaceFixture(semester.Id, targetClass.Id, team1.Id, team2.Id, assignment.Id, current.Id, replacement.Id, academic.Id);
    }

    private async Task<(HttpStatusCode Status, MentorAllocationPreviewResponse? Data)> PreviewWithEditsAsync(
        string token, ReplaceFixture fixture, params MentorAllocationEdit[] edits)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/preview")
        {
            Content = JsonContent.Create(new PreviewMentorAllocationRequest { SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Seed = 9, Edits = edits })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>();
        return (response.StatusCode, body?.Data);
    }

    private async Task<HttpResponseMessage> CommitAsync(string token, Guid sessionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/commit")
        {
            Content = JsonContent.Create(new CommitMentorAllocationRequest { SessionId = sessionId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task ManualEdits_ShouldNotOverwriteAnOccupiedSlotUnlessTheMentorIsExplicitlyReplaced()
    {
        var token = await GetAdminTokenAsync();
        var fixture = await SeedReplaceFixtureAsync(2061);

        var conflictOnly = await PreviewWithEditsAsync(token, fixture,
            new MentorAllocationEdit { TeamId = fixture.Team1, MentorType = nameof(MentorType.Enterprise), MentorProfileId = fixture.NewMentor });
        conflictOnly.Status.Should().Be(HttpStatusCode.OK);
        var conflict = conflictOnly.Data!.Conflicts.Should().ContainSingle().Subject;
        conflict.Kind.Should().Be("SlotOccupied");
        conflict.CurrentAssignmentId.Should().Be(fixture.CurrentAssignmentId);
        conflict.ProposedMentorProfileId.Should().Be(fixture.NewMentor);
        conflictOnly.Data.Assignments.Should().NotContain(item => item.TeamId == fixture.Team1 && item.MentorType == nameof(MentorType.Enterprise));
        conflictOnly.Data.ReplacementCount.Should().Be(0);

        var withoutReason = await PreviewWithEditsAsync(token, fixture,
            new MentorAllocationEdit { TeamId = fixture.Team1, MentorType = nameof(MentorType.Enterprise), MentorProfileId = fixture.NewMentor, Replace = true });
        withoutReason.Data!.Conflicts.Should().ContainSingle().Which.Kind.Should().Be("EditRejected");
        withoutReason.Data.ReplacementCount.Should().Be(0);

        var invalidType = await PreviewWithEditsAsync(token, fixture,
            new MentorAllocationEdit { TeamId = fixture.Team1, MentorType = "Nonsense", MentorProfileId = fixture.NewMentor });
        invalidType.Status.Should().Be(HttpStatusCode.BadRequest);

        using var verifyScope = factory.Services.CreateScope();
        var stillActive = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.CurrentAssignmentId);
        stillActive.Status.Should().Be(MentorAssignmentStatus.Active);
        stillActive.EndedAt.Should().BeNull();
    }

    [Fact]
    public async Task ReplacingAMentor_ShouldEndTheOldAssignmentAndCreateTheNewOneTogether()
    {
        var token = await GetAdminTokenAsync();
        var fixture = await SeedReplaceFixtureAsync(2062);

        var preview = await PreviewWithEditsAsync(token, fixture,
            new MentorAllocationEdit { TeamId = fixture.Team1, MentorType = nameof(MentorType.Enterprise), MentorProfileId = fixture.NewMentor, Replace = true, Reason = "Mentor is no longer available" },
            new MentorAllocationEdit { TeamId = fixture.Team2, MentorType = nameof(MentorType.Academic), MentorProfileId = null });
        preview.Status.Should().Be(HttpStatusCode.OK);
        var data = preview.Data!;
        data.CanCommit.Should().BeTrue();
        data.ReplacementCount.Should().Be(1);

        var replacement = data.Assignments.Single(item => item.TeamId == fixture.Team1 && item.MentorType == nameof(MentorType.Enterprise));
        replacement.Source.Should().Be(MentorAllocationSources.Manual);
        replacement.MentorProfileId.Should().Be(fixture.NewMentor);
        replacement.ReplacesAssignmentId.Should().Be(fixture.CurrentAssignmentId);
        // The replaced mentor loses Team 1; whatever else Balanced gives them afterwards is counted on top of that.
        var replacedLoad = data.MentorLoads.Single(item => item.MentorProfileId == fixture.CurrentMentor);
        replacedLoad.TotalBefore.Should().Be(1);
        replacedLoad.Subjects.Sum(item => item.Removed).Should().Be(1);
        replacedLoad.TotalAfter.Should().Be(1 - 1 + data.Assignments.Count(item => item.MentorProfileId == fixture.CurrentMentor));
        data.Assignments.Should().NotContain(item => item.TeamId == fixture.Team1 && item.MentorProfileId == fixture.CurrentMentor);
        data.MentorLoads.Single(item => item.MentorProfileId == fixture.NewMentor).TotalAfter.Should().BeGreaterThanOrEqualTo(1);

        // Team 1 still gets its missing Academic mentor and Team 2 its Enterprise one, but Team 2's Academic slot was left empty on purpose.
        data.Assignments.Should().Contain(item => item.TeamId == fixture.Team1 && item.MentorType == nameof(MentorType.Academic));
        data.Assignments.Should().NotContain(item => item.TeamId == fixture.Team2 && item.MentorType == nameof(MentorType.Academic));
        data.Unfilled.Should().ContainSingle(item => item.TeamId == fixture.Team2 && item.MentorType == nameof(MentorType.Academic));

        var commit = await CommitAsync(token, data.SessionId);
        commit.StatusCode.Should().Be(HttpStatusCode.OK);
        var committed = await commit.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationCommitResponse>>();
        committed!.Data!.EndedCount.Should().Be(1);

        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var old = await verifyContext.MentorAssignments.AsNoTracking().SingleAsync(item => item.Id == fixture.CurrentAssignmentId);
        old.Status.Should().Be(MentorAssignmentStatus.Ended);
        old.EndedAt.Should().NotBeNull();
        old.Note.Should().Contain("Mentor is no longer available");

        var active = await verifyContext.MentorAssignments.AsNoTracking()
            .Where(item => item.TeamId == fixture.Team1 && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null).ToListAsync();
        active.Should().ContainSingle(item => item.Slot == MentorType.Enterprise && item.MentorProfileId == fixture.NewMentor);
        active.Where(item => item.Slot == MentorType.Enterprise).Should().HaveCount(1);
        (await verifyContext.MentorAssignments.AsNoTracking().AnyAsync(item => item.TeamId == fixture.Team2 && item.Slot == MentorType.Academic)).Should().BeFalse();
    }

    [Fact]
    public async Task ReplacingAMentor_ShouldBeRejectedWhenTheCurrentMentorChangedAfterThePreview()
    {
        var token = await GetAdminTokenAsync();
        var fixture = await SeedReplaceFixtureAsync(2063);

        var preview = await PreviewWithEditsAsync(token, fixture,
            new MentorAllocationEdit { TeamId = fixture.Team1, MentorType = nameof(MentorType.Enterprise), MentorProfileId = fixture.NewMentor, Replace = true, Reason = "Swap requested" });
        preview.Data!.CanCommit.Should().BeTrue();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var assignment = await context.MentorAssignments.SingleAsync(item => item.Id == fixture.CurrentAssignmentId);
            assignment.Status = MentorAssignmentStatus.Ended;
            assignment.EndedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        var commit = await CommitAsync(token, preview.Data.SessionId);

        commit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var verifyScope = factory.Services.CreateScope();
        (await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking()
            .AnyAsync(item => item.TeamId == fixture.Team1 && item.MentorProfileId == fixture.NewMentor)).Should().BeFalse("nothing may be saved when the preview is stale");
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

    private static byte[] CreateMentorWorkbook(
        string enterpriseEmail,
        string academicEmail,
        string enterpriseName = "Enterprise Integration Mentor",
        string academicName = "Academic Integration Mentor")
    {
        using var workbook = new XLWorkbook();
        var enterprise = workbook.Worksheets.Add("DS Mentor_FA26");
        string[] enterpriseHeaders = ["STT", "Họ và tên", "Ngày tháng năm sinh", "SDT", "Loại HĐ", "Trình độ học vấn", "Địa chỉ hiện nay", "Email", "Fpt Email", "Vị trí, Chức danh", "Công ty"];
        for (var index = 0; index < enterpriseHeaders.Length; index++) enterprise.Cell(1, index + 1).Value = enterpriseHeaders[index];
        string[] enterpriseValues = ["1", enterpriseName, "21/01/1993", "0900000000", "Thỉnh giảng", "Thạc sĩ", "Đà Nẵng", enterpriseEmail, "", "CEO", "Integration Company"];
        for (var index = 0; index < enterpriseValues.Length; index++) enterprise.Cell(2, index + 1).Value = enterpriseValues[index];

        var academic = workbook.Worksheets.Add("Mentor IT_FA26");
        string[] academicHeaders = ["STT", "Email công việc", "Họ tên", "Phòng ban trực tiếp", "Chức danh (VN)"];
        for (var index = 0; index < academicHeaders.Length; index++) academic.Cell(1, index + 1).Value = academicHeaders[index];
        string[] academicValues = ["1", academicEmail, academicName, "Bộ môn CNTT", "Giảng viên"];
        for (var index = 0; index < academicValues.Length; index++) academic.Cell(2, index + 1).Value = academicValues[index];

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CreateNameOnlyMentorWorkbook(string enterpriseName, string academicName)
    {
        using var workbook = new XLWorkbook();
        var enterprise = workbook.Worksheets.Add("DS Mentor_FA26");
        enterprise.Cell(1, 1).Value = "STT";
        enterprise.Cell(1, 2).Value = "Họ và tên";
        enterprise.Cell(2, 1).Value = 1;
        enterprise.Cell(2, 2).Value = enterpriseName;

        var academic = workbook.Worksheets.Add("Mentor IT_FA26");
        academic.Cell(1, 1).Value = "STT";
        academic.Cell(1, 2).Value = "Họ tên";
        academic.Cell(2, 1).Value = 1;
        academic.Cell(2, 2).Value = academicName;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] CreateIdentityOnlyMentorWorkbook(
        string enterpriseEmail,
        string academicEmail,
        string enterpriseName,
        string academicName)
    {
        using var workbook = new XLWorkbook();
        var enterprise = workbook.Worksheets.Add("DS Mentor_FA26");
        string[] enterpriseHeaders = ["STT", "Họ và tên", "Email"];
        string[] enterpriseValues = ["1", enterpriseName, enterpriseEmail];
        for (var index = 0; index < enterpriseHeaders.Length; index++)
        {
            enterprise.Cell(1, index + 1).Value = enterpriseHeaders[index];
            enterprise.Cell(2, index + 1).Value = enterpriseValues[index];
        }

        var academic = workbook.Worksheets.Add("Mentor IT_FA26");
        string[] academicHeaders = ["STT", "Email công việc", "Họ tên"];
        string[] academicValues = ["1", academicEmail, academicName];
        for (var index = 0; index < academicHeaders.Length; index++)
        {
            academic.Cell(1, index + 1).Value = academicHeaders[index];
            academic.Cell(2, index + 1).Value = academicValues[index];
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
