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
        preview.Data.Strategy.Should().Be(MentorAllocationStrategies.Balanced, "Balanced is the default when no strategy is sent");
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
        Guid retainedEnterpriseId, retainedAcademicId, droppedEnterpriseId, spareEnterpriseId, inactiveAcademicId, continuingTwoId;
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
            var inactiveSource = NewTeam(previousClass, $"P101{suffix}_G3");
            var continuingTwo = NewTeam(targetClass, $"T201{suffix}_G3", inactiveSource);
            context.Teams.AddRange(continuingSource, droppedSource, continuing, fresh, inactiveSource, continuingTwo);

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
            var spareEnterprise = NewMentor(MentorType.Enterprise, "ent-spare", true);
            NewMentor(MentorType.Academic, "acad-spare", true);
            var inactiveAcademic = NewMentor(MentorType.Academic, "acad-inactive", false);

            MentorAssignment EndedAtCompletion(Team team, MentorProfile mentor) => new()
            {
                TeamId = team.Id, Team = team, MentorProfileId = mentor.Id, MentorProfile = mentor, AssignedById = adminId,
                AssignedAt = completedAt.AddDays(-60), EndedAt = completedAt, Slot = mentor.Type, Status = MentorAssignmentStatus.Ended, CreatedBy = adminId
            };
            context.MentorAssignments.AddRange(
                EndedAtCompletion(continuingSource, retainedEnterprise),
                EndedAtCompletion(continuingSource, retainedAcademic),
                EndedAtCompletion(droppedSource, droppedEnterprise),
                EndedAtCompletion(inactiveSource, retainedEnterprise),
                EndedAtCompletion(inactiveSource, inactiveAcademic));
            await context.SaveChangesAsync();

            targetSemesterId = targetSemester.Id;
            targetClassId = targetClass.Id;
            continuingTeamId = continuing.Id;
            newTeamId = fresh.Id;
            retainedEnterpriseId = retainedEnterprise.Id;
            retainedAcademicId = retainedAcademic.Id;
            droppedEnterpriseId = droppedEnterprise.Id;
            spareEnterpriseId = spareEnterprise.Id;
            inactiveAcademicId = inactiveAcademic.Id;
            continuingTwoId = continuingTwo.Id;
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
        preview.Data.RetainedCount.Should().Be(3);
        preview.Data.Assignments.Should().HaveCount(6);

        var continuingRows = preview.Data.Assignments.Where(item => item.TeamId == continuingTeamId).ToArray();
        continuingRows.Should().OnlyContain(item => item.Source == MentorAllocationSources.Retained);
        continuingRows.Single(item => item.MentorType == nameof(MentorType.Enterprise)).MentorProfileId.Should().Be(retainedEnterpriseId);
        continuingRows.Single(item => item.MentorType == nameof(MentorType.Academic)).MentorProfileId.Should().Be(retainedAcademicId);

        var freshRows = preview.Data.Assignments.Where(item => item.TeamId == newTeamId).ToArray();
        freshRows.Should().HaveCount(2).And.OnlyContain(item => item.Source == MentorAllocationSources.Allocated);
        freshRows.Should().NotContain(item => item.MentorProfileId == retainedEnterpriseId, "the retained mentor already carries a team and the spare mentor has none");

        // A mentor who is not active this semester is not carried over: the slot is flagged and filled by the allocation instead.
        var continuingTwoRows = preview.Data.Assignments.Where(item => item.TeamId == continuingTwoId).ToArray();
        continuingTwoRows.Should().HaveCount(2);
        var keptOnSecondTeam = continuingTwoRows.Single(item => item.MentorType == nameof(MentorType.Enterprise));
        keptOnSecondTeam.Source.Should().Be(MentorAllocationSources.Retained);
        keptOnSecondTeam.MentorProfileId.Should().Be(retainedEnterpriseId, "a mentor of several continuing teams keeps all of them");
        var replacedAcademic = continuingTwoRows.Single(item => item.MentorType == nameof(MentorType.Academic));
        replacedAcademic.Source.Should().Be(MentorAllocationSources.Allocated);
        replacedAcademic.MentorProfileId.Should().NotBe(inactiveAcademicId);
        preview.Data.Assignments.Should().NotContain(item => item.MentorProfileId == inactiveAcademicId);

        preview.Data.Skipped.Should().HaveCount(2);
        var dropped = preview.Data.Skipped.Single(item => item.Reason == "NoContinuedTeam");
        dropped.MentorProfileId.Should().Be(droppedEnterpriseId);
        var inactive = preview.Data.Skipped.Single(item => item.Reason == "MentorNotActiveInSemester");
        inactive.MentorProfileId.Should().Be(inactiveAcademicId);
        inactive.TeamId.Should().Be(continuingTwoId);

        // A hand edit wins over the mentor that would have been kept, and the preview stays unsaved until it is confirmed.
        var edited = await PostPreviewAsync(token, new PreviewMentorAllocationRequest
        {
            SemesterId = targetSemesterId, ClassIds = [targetClassId], Seed = 7,
            Edits = [new MentorAllocationEdit { TeamId = continuingTeamId, MentorType = nameof(MentorType.Enterprise), MentorProfileId = spareEnterpriseId }]
        });
        edited.Status.Should().Be(HttpStatusCode.OK);
        var overridden = edited.Data!.Assignments.Single(item => item.TeamId == continuingTeamId && item.MentorType == nameof(MentorType.Enterprise));
        overridden.Source.Should().Be(MentorAllocationSources.Manual);
        overridden.MentorProfileId.Should().Be(spareEnterpriseId);
        edited.Data.RetainedCount.Should().Be(2);
        using (var unsavedScope = factory.Services.CreateScope())
        {
            var unsaved = unsavedScope.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking();
            (await unsaved.CountAsync(item => item.Status == MentorAssignmentStatus.Active &&
                (item.TeamId == continuingTeamId || item.TeamId == newTeamId || item.TeamId == continuingTwoId))).Should().Be(0);
        }

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
            // A mentor who exists in the master list but is not active in this semester must never be chosen.
            var idleEmail = $"random-idle-{suffix}@example.com";
            var idleUser = new User { FullName = "Random Idle Enterprise", Email = idleEmail, NormalizedEmail = idleEmail, PasswordHash = "not-used", Status = UserStatus.Active };
            context.Users.Add(idleUser);
            context.UserRoles.Add(new UserRole { UserId = idleUser.Id, User = idleUser, RoleId = mentorRole.Id, Role = mentorRole, AssignedAt = DateTime.UtcNow, AssignedBy = adminId });
            context.MentorProfiles.Add(new MentorProfile { UserId = idleUser.Id, User = idleUser, Type = MentorType.Enterprise, Status = MentorProfileStatus.Active, CreatedBy = adminId });
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
        data.MentorLoads.Should().HaveCount(2, "the mentor who is not active this semester is not listed");
        data.MentorLoads.Should().OnlyContain(item => item.ContractType == "Thỉnh giảng" && item.TotalBefore == 0);
        data.MentorLoads.Sum(item => item.TotalAfter).Should().Be(3);
        data.Assignments.Should().NotContain(item => item.MentorEmail.Contains("random-idle"));

        // A preview alone never changes the official data.
        using (var beforeCommit = factory.Services.CreateScope())
        {
            (await beforeCommit.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking()
                .CountAsync(item => teamIds.Contains(item.TeamId))).Should().Be(0);
        }

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
        var occupied = conflictOnly.Data.ExistingAssignments.Should().ContainSingle(item => item.TeamId == fixture.Team1).Subject;
        occupied.AssignmentId.Should().Be(fixture.CurrentAssignmentId);
        occupied.MentorProfileId.Should().Be(fixture.CurrentMentor);
        occupied.Replaced.Should().BeFalse();

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
        data.ExistingAssignments.Should().ContainSingle(item => item.AssignmentId == fixture.CurrentAssignmentId).Which.Replaced.Should().BeTrue();

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

    private async Task<(HttpStatusCode Status, MentorAllocationPreviewResponse? Data)> PostPreviewAsync(string token, PreviewMentorAllocationRequest payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/allocations/preview") { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        var body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>()
            : null;
        return (response.StatusCode, body?.Data);
    }

    [Fact]
    public async Task AllocationEndpoints_ShouldBeDeniedWithoutAnAdministrator()
    {
        var preview = new PreviewMentorAllocationRequest { SemesterId = Guid.NewGuid() };
        var commit = new CommitMentorAllocationRequest { SessionId = Guid.NewGuid() };

        using var anonymousPreview = await _client.PostAsJsonAsync("/api/admin/mentors/allocations/preview", preview);
        using var anonymousCommit = await _client.PostAsJsonAsync("/api/admin/mentors/allocations/commit", commit);
        anonymousPreview.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        anonymousCommit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        string lecturerToken;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lecturerRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Lecturer);
            var email = $"allocation-denied-{Guid.NewGuid():N}@example.com";
            var lecturer = new User { FullName = "Allocation Denied Lecturer", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
            context.Users.Add(lecturer);
            context.UserRoles.Add(new UserRole { UserId = lecturer.Id, User = lecturer, RoleId = lecturerRole.Id, Role = lecturerRole, AssignedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
            lecturerToken = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token;
        }

        (await PostPreviewAsync(lecturerToken, preview)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await CommitAsync(lecturerToken, commit.SessionId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Commit_ShouldCreateEachAssignmentOnceWhenConfirmedTwiceAtTheSameTime()
    {
        var token = await GetAdminTokenAsync();
        var fixture = await SeedReplaceFixtureAsync(2072);
        var preview = await PreviewWithEditsAsync(token, fixture);
        preview.Data!.Assignments.Should().HaveCount(3);

        var responses = await Task.WhenAll(CommitAsync(token, preview.Data.SessionId), CommitAsync(token, preview.Data.SessionId));
        var third = await CommitAsync(token, preview.Data.SessionId);

        responses.Count(item => item.StatusCode == HttpStatusCode.OK).Should().Be(1, "only one of two simultaneous confirmations may save");
        third.StatusCode.Should().NotBe(HttpStatusCode.OK, "a confirmed preview cannot be saved again");
        using var scope = factory.Services.CreateScope();
        var active = await scope.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking()
            .Where(item => (item.TeamId == fixture.Team1 || item.TeamId == fixture.Team2) && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null)
            .ToListAsync();
        active.Should().HaveCount(4, "the one existing assignment plus the three new ones, with no duplicates");
        active.GroupBy(item => (item.TeamId, item.Slot)).Should().OnlyContain(group => group.Count() == 1);
    }

    [Fact]
    public async Task Commit_ShouldBeRejectedWhenAMentorLoadChangedAfterThePreview()
    {
        var token = await GetAdminTokenAsync();
        var fixture = await SeedReplaceFixtureAsync(2073);
        var preview = await PreviewWithEditsAsync(token, fixture);
        preview.Data!.Assignments.Should().Contain(item => item.MentorProfileId == fixture.NewMentor);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminId = await context.Users.Where(item => item.NormalizedEmail == "admin@ehub.test").Select(item => item.Id).SingleAsync();
            var team = await context.Teams.SingleAsync(item => item.Id == fixture.Team1);
            var extraTeam = new Team { ClassId = team.ClassId, TeamCode = $"{team.TeamCode}_EXTRA", TeamName = "Extra", Status = TeamStatus.Active, CreatedById = adminId };
            context.Teams.Add(extraTeam);
            context.MentorAssignments.Add(new MentorAssignment
            {
                TeamId = extraTeam.Id, MentorProfileId = fixture.NewMentor, AssignedById = adminId, AssignedAt = DateTime.UtcNow,
                Slot = MentorType.Enterprise, Status = MentorAssignmentStatus.Active, CreatedBy = adminId
            });
            await context.SaveChangesAsync();
        }

        var commit = await CommitAsync(token, preview.Data.SessionId);

        commit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var verify = factory.Services.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.AsNoTracking()
            .CountAsync(item => item.TeamId == fixture.Team1 || item.TeamId == fixture.Team2)).Should().Be(1, "only the assignment that already existed remains");
    }

    [Fact]
    public async Task MasterImportPreview_ShouldReturn401_WhenNoTokenIsProvided()
    {
        using var request = CreateMasterPreviewRequest(CreateMentorWorkbook("master-401-e@example.com", "master-401-a@example.com"));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MasterImportPreview_ShouldReturn403_WhenLecturerTokenIsProvided()
    {
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lecturerRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Lecturer);
            var email = $"master-forbidden-{Guid.NewGuid():N}@example.com";
            var lecturer = new User { FullName = "Master Forbidden Lecturer", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
            context.Users.Add(lecturer);
            context.UserRoles.Add(new UserRole { UserId = lecturer.Id, User = lecturer, RoleId = lecturerRole.Id, Role = lecturerRole, AssignedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
            token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token;
        }
        using var request = CreateMasterPreviewRequest(CreateMentorWorkbook("master-403-e@example.com", "master-403-a@example.com"), token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MasterImport_ShouldCreateMentors_WithoutAddingThemToAnySemester()
    {
        var token = await GetAdminTokenAsync();
        var enterpriseEmail = $"master-enterprise-{Guid.NewGuid():N}@example.com";
        var academicEmail = $"master-academic-{Guid.NewGuid():N}@example.edu.vn";
        using var previewRequest = CreateMasterPreviewRequest(CreateMentorWorkbook(enterpriseEmail, academicEmail), token);

        var previewResponse = await _client.SendAsync(previewRequest);
        var preview = await previewResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();

        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Data.CanCommit.Should().BeTrue();
        preview.Data.CreateCount.Should().Be(2);

        var commitResponse = await CommitImportAsync(token, preview.Data.SessionId);
        var commit = await commitResponse.Content.ReadFromJsonAsync<ApiResponse<MentorImportCommitResponse>>();

        commitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        commit!.Data!.CreatedCount.Should().Be(2);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profiles = await context.MentorProfiles.AsNoTracking().Include(item => item.User)
            .Where(item => item.User.NormalizedEmail == enterpriseEmail || item.User.NormalizedEmail == academicEmail).ToListAsync();
        profiles.Should().HaveCount(2);
        var userIds = profiles.Select(item => item.UserId).ToArray();
        (await context.SemesterStaffAssignments.AsNoTracking().CountAsync(item => userIds.Contains(item.UserId)))
            .Should().Be(0, "the master list does not put anyone into a semester");
    }

    [Fact]
    public async Task MasterImport_ShouldUpdateExistingMentor_EvenWhenNotInAnySemester()
    {
        var token = await GetAdminTokenAsync();
        var enterpriseEmail = $"master-update-{Guid.NewGuid():N}@example.com";
        var academicEmail = $"master-update-a-{Guid.NewGuid():N}@example.edu.vn";
        using var firstPreview = CreateMasterPreviewRequest(CreateMentorWorkbook(enterpriseEmail, academicEmail), token);
        var first = await (await _client.SendAsync(firstPreview)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();
        (await CommitImportAsync(token, first!.Data!.SessionId)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var secondPreview = CreateMasterPreviewRequest(CreateMentorWorkbook(enterpriseEmail, academicEmail, enterpriseName: "Renamed Enterprise Mentor"), token);
        var second = await (await _client.SendAsync(secondPreview)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();

        second!.Data!.CanCommit.Should().BeTrue();
        second.Data.CreateCount.Should().Be(0);
        second.Data.UpdateCount.Should().Be(2);
        var commit = await (await CommitImportAsync(token, second.Data.SessionId)).Content.ReadFromJsonAsync<ApiResponse<MentorImportCommitResponse>>();
        commit!.Data!.UpdatedCount.Should().Be(2);
    }

    [Fact]
    public async Task MasterImport_ShouldAcceptRowsWithoutEmail_AndKeepThemAsIncompleteMasterMentors()
    {
        var token = await GetAdminTokenAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var enterpriseName = $"Master Incomplete Enterprise {suffix}";
        var academicName = $"Master Incomplete Academic {suffix}";
        using var request = CreateMasterPreviewRequest(CreateNameOnlyMentorWorkbook(enterpriseName, academicName), token);

        var response = await _client.SendAsync(request);
        var preview = await response.Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Data.CanCommit.Should().BeTrue("missing email and other fields must not block the import");
        preview.Data.ErrorCount.Should().Be(0);
        preview.Data.NeedsCompletionCount.Should().Be(2);
        preview.Data.Rows.Should().OnlyContain(row => row.IsValid && row.Status == "NeedsCompletion");

        var commit = await (await CommitImportAsync(token, preview.Data.SessionId)).Content.ReadFromJsonAsync<ApiResponse<MentorImportCommitResponse>>();
        commit!.Data!.DraftSavedCount.Should().Be(2);
        commit.Data.CreatedCount.Should().Be(0);

        var list = await GetIncompleteMastersAsync(token, suffix);
        list.Should().Contain(item => item.FullName == enterpriseName && item.MentorType == "Enterprise" && item.MissingFields.Contains("Email"));
        list.Should().Contain(item => item.FullName == academicName && item.MentorType == "Academic");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.MentorImportDrafts.AsNoTracking().Where(item => item.FullName == enterpriseName || item.FullName == academicName).AllAsync(item => item.SemesterId == null))
            .Should().BeTrue("master drafts belong to no semester");
    }

    [Fact]
    public async Task MasterImport_ShouldCompleteAnIncompleteMasterMentor_WhenALaterFileProvidesTheEmail()
    {
        var token = await GetAdminTokenAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var enterpriseName = $"Master Complete Enterprise {suffix}";
        var academicName = $"Master Complete Academic {suffix}";
        var enterpriseEmail = $"master-complete-e-{suffix}@example.com";
        var academicEmail = $"master-complete-a-{suffix}@example.edu.vn";
        using var draftRequest = CreateMasterPreviewRequest(CreateNameOnlyMentorWorkbook(enterpriseName, academicName), token);
        var draftPreview = await (await _client.SendAsync(draftRequest)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();
        (await CommitImportAsync(token, draftPreview!.Data!.SessionId)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var fullRequest = CreateMasterPreviewRequest(CreateMentorWorkbook(enterpriseEmail, academicEmail, enterpriseName, academicName), token);
        var fullPreview = await (await _client.SendAsync(fullRequest)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();

        fullPreview!.Data!.CanCommit.Should().BeTrue();
        fullPreview.Data.CompleteDraftCount.Should().Be(2);
        var commit = await (await CommitImportAsync(token, fullPreview.Data.SessionId)).Content.ReadFromJsonAsync<ApiResponse<MentorImportCommitResponse>>();
        commit!.Data!.CreatedCount.Should().Be(2);
        commit.Data.DraftCompletedCount.Should().Be(2);
        (await GetIncompleteMastersAsync(token, suffix)).Should().NotContain(item => item.FullName == enterpriseName || item.FullName == academicName);
    }

    [Fact]
    public async Task MasterImport_ShouldNotEraseExistingProfileFields_WhenALaterFileOmitsThem()
    {
        var token = await GetAdminTokenAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var enterpriseEmail = $"keep-fields-e-{suffix}@example.com";
        var academicEmail = $"keep-fields-a-{suffix}@example.edu.vn";
        var enterpriseName = $"Keep Fields Enterprise {suffix}";
        var academicName = $"Keep Fields Academic {suffix}";
        using var fullRequest = CreateMasterPreviewRequest(CreateMentorWorkbook(enterpriseEmail, academicEmail, enterpriseName, academicName), token);
        var full = await (await _client.SendAsync(fullRequest)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();
        (await CommitImportAsync(token, full!.Data!.SessionId)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var minimalRequest = CreateMasterPreviewRequest(CreateIdentityOnlyMentorWorkbook(enterpriseEmail, academicEmail, enterpriseName, academicName), token);
        var minimal = await (await _client.SendAsync(minimalRequest)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();
        minimal!.Data!.CanCommit.Should().BeTrue();
        (await CommitImportAsync(token, minimal.Data.SessionId)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profiles = await context.MentorProfiles.AsNoTracking().Include(item => item.User)
            .Where(item => item.User.NormalizedEmail == enterpriseEmail || item.User.NormalizedEmail == academicEmail).ToListAsync();
        profiles.Should().HaveCount(2);
        profiles.Single(item => item.Type == MentorType.Enterprise).Organization.Should().Be("Integration Company");
        profiles.Single(item => item.Type == MentorType.Academic).Department.Should().Be("Bộ môn CNTT");
    }

    [Fact]
    public async Task MasterImport_ShouldCompleteAnIncompleteMentorSavedEarlierForASemester()
    {
        var token = await GetAdminTokenAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var enterpriseName = $"Legacy Draft Enterprise {suffix}";
        var academicName = $"Legacy Draft Academic {suffix}";
        Guid draftId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var semesterId = await context.Semesters.AsNoTracking().Select(item => item.Id).FirstAsync();
            var draft = new MentorImportDraft
            {
                SemesterId = semesterId, Type = MentorType.Enterprise, FullName = enterpriseName,
                NormalizedFullName = enterpriseName.ToLowerInvariant(), Status = MentorImportDraftStatus.NeedsCompletion
            };
            context.MentorImportDrafts.Add(draft);
            await context.SaveChangesAsync();
            draftId = draft.Id;
        }
        var email = $"legacy-draft-e-{suffix}@example.com";
        using var request = CreateMasterPreviewRequest(CreateMentorWorkbook(email, $"legacy-draft-a-{suffix}@example.edu.vn", enterpriseName, academicName), token);
        var preview = await (await _client.SendAsync(request)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();

        preview!.Data!.CanCommit.Should().BeTrue();
        preview.Data.CompleteDraftCount.Should().Be(1, "only the enterprise mentor has an earlier incomplete record");
        (await CommitImportAsync(token, preview.Data.SessionId)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var verify = factory.Services.CreateScope();
        var saved = await verify.ServiceProvider.GetRequiredService<AppDbContext>().MentorImportDrafts.AsNoTracking().SingleAsync(item => item.Id == draftId);
        saved.Status.Should().Be(MentorImportDraftStatus.Converted);
        saved.ConvertedMentorProfileId.Should().NotBeNull();
    }

    [Fact]
    public async Task IncompleteMasterMentors_ShouldPageSearchAndFilterByType()
    {
        var token = await GetAdminTokenAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var request = CreateMasterPreviewRequest(CreateNameOnlyMentorWorkbook($"Paged Industry {suffix}", $"Paged Lecturer {suffix}"), token);
        var preview = await (await _client.SendAsync(request)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();
        (await CommitImportAsync(token, preview!.Data!.SessionId)).StatusCode.Should().Be(HttpStatusCode.OK);

        async Task<IncompleteMentorListResponse> Get(string query)
        {
            using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/mentors/incomplete?{query}");
            get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client.SendAsync(get);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<IncompleteMentorListResponse>>())!.Data!;
        }

        var all = await Get($"search={suffix}&limit=1");
        all.Pagination.Total.Should().Be(2);
        all.Pagination.Pages.Should().Be(2);
        all.Mentors.Should().HaveCount(1);
        (await Get($"search={suffix}&mentorType=Enterprise")).Mentors.Should().ContainSingle().Which.MentorType.Should().Be("Enterprise");
        (await Get($"search={suffix}&mentorType=academic")).Mentors.Should().ContainSingle().Which.MentorType.Should().Be("Academic");
        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/api/admin/mentors/incomplete?mentorType=Unknown");
        invalid.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await _client.SendAsync(invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IncompleteMasterMentors_ShouldCountEachPersonOnce_AndCompleteEveryRecordOfThem()
    {
        var token = await GetAdminTokenAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var enterpriseName = $"Twice Saved Enterprise {suffix}";
        var academicName = $"Twice Saved Academic {suffix}";
        // One record saved by the retired per-semester import...
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var semesterId = await context.Semesters.AsNoTracking().Select(item => item.Id).FirstAsync();
            context.MentorImportDrafts.Add(new MentorImportDraft
            {
                SemesterId = semesterId, Type = MentorType.Enterprise, FullName = enterpriseName,
                NormalizedFullName = enterpriseName.ToLowerInvariant(), Status = MentorImportDraftStatus.NeedsCompletion
            });
            await context.SaveChangesAsync();
        }
        // ...and a second record for the same person saved by a master-list import.
        using var draftRequest = CreateMasterPreviewRequest(CreateNameOnlyMentorWorkbook(enterpriseName, academicName), token);
        var draftPreview = await (await _client.SendAsync(draftRequest)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();
        draftPreview!.Data!.CanCommit.Should().BeTrue("a person with an earlier record must not block the import");
        (await CommitImportAsync(token, draftPreview.Data.SessionId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await GetIncompleteMastersAsync(token, suffix);
        list.Should().HaveCount(2, "each person is listed once even when two records exist");

        using var fullRequest = CreateMasterPreviewRequest(CreateMentorWorkbook($"twice-e-{suffix}@example.com", $"twice-a-{suffix}@example.edu.vn", enterpriseName, academicName), token);
        var full = await (await _client.SendAsync(fullRequest)).Content.ReadFromJsonAsync<ApiResponse<MentorImportPreviewResponse>>();
        full!.Data!.CanCommit.Should().BeTrue();
        (await CommitImportAsync(token, full.Data.SessionId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetIncompleteMastersAsync(token, suffix)).Should().BeEmpty("every record of a completed person is closed");
        using var verify = factory.Services.CreateScope();
        (await verify.ServiceProvider.GetRequiredService<AppDbContext>().MentorImportDrafts.AsNoTracking()
            .AnyAsync(item => item.FullName == enterpriseName && item.Status == MentorImportDraftStatus.NeedsCompletion)).Should().BeFalse();
    }

    [Fact]
    public async Task IncompleteMasterMentors_ShouldReturn401And403_WhenNotAnAdmin()
    {
        (await _client.GetAsync("/api/admin/mentors/incomplete")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lecturerRole = await context.Roles.SingleAsync(role => role.Name == SystemRoles.Lecturer);
            var email = $"incomplete-forbidden-{Guid.NewGuid():N}@example.com";
            var lecturer = new User { FullName = "Incomplete Forbidden Lecturer", Email = email, NormalizedEmail = email, PasswordHash = "not-used", Status = UserStatus.Active };
            context.Users.Add(lecturer);
            context.UserRoles.Add(new UserRole { UserId = lecturer.Id, User = lecturer, RoleId = lecturerRole.Id, Role = lecturerRole, AssignedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
            token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token;
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/mentors/incomplete");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<IReadOnlyCollection<IncompleteMentorResponse>> GetIncompleteMastersAsync(string token, string search)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/mentors/incomplete?limit=100&search={Uri.EscapeDataString(search)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<IncompleteMentorListResponse>>())!.Data!.Mentors;
    }

    private Task<HttpResponseMessage> CommitImportAsync(string token, Guid sessionId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/imports/commit")
        {
            Content = JsonContent.Create(new CommitMentorImportRequest { SessionId = sessionId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static HttpRequestMessage CreateMasterPreviewRequest(byte[] workbook, string? token = null)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "mentors.xlsx");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/mentors/master-imports/preview") { Content = content };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = "admin@ehub.test", Password = "Admin@123456" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    // A mentor workbook with two real mentors plus whatever leftovers the caller adds to the enterprise sheet.
    private static byte[] CreateWorkbookWithLeftovers(Action<IXLWorksheet> addLeftovers)
    {
        using var workbook = new XLWorkbook();
        var enterprise = workbook.Worksheets.Add("DS Mentor_FA26");
        enterprise.Cell(1, 1).Value = "STT";
        enterprise.Cell(1, 2).Value = "Họ và tên";
        enterprise.Cell(1, 8).Value = "Email";
        for (var row = 2; row <= 3; row++)
        {
            enterprise.Cell(row, 1).Value = row - 1;
            enterprise.Cell(row, 2).Value = $"Leftover Mentor {Guid.NewGuid():N}";
            enterprise.Cell(row, 8).Value = $"leftover-{Guid.NewGuid():N}@example.com";
        }
        addLeftovers(enterprise);

        var academic = workbook.Worksheets.Add("Mentor IT_FA26");
        academic.Cell(1, 1).Value = "STT";
        academic.Cell(1, 2).Value = "Họ tên";
        academic.Cell(2, 1).Value = 1;
        academic.Cell(2, 2).Value = $"Leftover Lecturer {Guid.NewGuid():N}";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<(HttpStatusCode Status, MentorImportPreviewResponse? Preview, string Body)> PreviewWorkbookAsync(byte[] workbook)
    {
        var token = await GetAdminTokenAsync();
        using var request = CreateMasterPreviewRequest(workbook, token);
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var preview = response.IsSuccessStatusCode
            ? System.Text.Json.JsonSerializer.Deserialize<ApiResponse<MentorImportPreviewResponse>>(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.Data
            : null;
        return (response.StatusCode, preview, body);
    }

    [Fact]
    public async Task MasterImportPreview_ShouldIgnoreLeftoverRows_BelowTheMentorList()
    {
        // Empty but formatted rows far below the data, as left by a re-saved Excel file or a Google Sheets export.
        var workbook = CreateWorkbookWithLeftovers(sheet =>
        {
            for (var row = 600; row <= 1000; row++) sheet.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.White;
        });

        var (status, preview, body) = await PreviewWorkbookAsync(workbook);

        status.Should().Be(HttpStatusCode.OK, body);
        preview!.TotalRows.Should().Be(3, "two enterprise mentors and one lecturer; the empty rows are not data");
        preview.ErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task MasterImportPreview_ShouldIgnoreRowsThatOnlyHaveARunningNumber()
    {
        var workbook = CreateWorkbookWithLeftovers(sheet =>
        {
            for (var row = 4; row <= 700; row++) sheet.Cell(row, 1).Value = row - 1;
        });

        var (status, preview, body) = await PreviewWorkbookAsync(workbook);

        status.Should().Be(HttpStatusCode.OK, body);
        preview!.TotalRows.Should().Be(3);
        preview.ErrorCount.Should().Be(0, "numbered rows without a mentor are not mentors");
    }

    [Fact]
    public async Task MasterImportPreview_ShouldIgnoreEmptyFormattedColumnsBeyondTheLimit()
    {
        var workbook = CreateWorkbookWithLeftovers(sheet => sheet.Cell(2, 40).Style.Fill.BackgroundColor = XLColor.White);

        var (status, preview, body) = await PreviewWorkbookAsync(workbook);

        status.Should().Be(HttpStatusCode.OK, body);
        preview!.TotalRows.Should().Be(3);
    }

    [Fact]
    public async Task MasterImportPreview_ShouldRejectDataInColumnsBeyondTheLimit_WithAClearMessage()
    {
        var workbook = CreateWorkbookWithLeftovers(sheet => sheet.Cell(2, 40).Value = "stray note");

        var (status, _, body) = await PreviewWorkbookAsync(workbook);

        status.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("at most 20 columns").And.Contain("MENTOR_IMPORT_FILE_INVALID");
    }

    [Fact]
    public async Task MasterImportPreview_ShouldRejectMoreThan500RealMentorRows_AndSayHowManyThereAre()
    {
        var workbook = CreateWorkbookWithLeftovers(sheet =>
        {
            for (var row = 4; row <= 502; row++)
            {
                sheet.Cell(row, 1).Value = row - 1;
                sheet.Cell(row, 2).Value = $"Bulk Mentor {row}";
            }
        });

        var (status, _, body) = await PreviewWorkbookAsync(workbook);

        status.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("501 mentor rows").And.Contain("at most 500");
    }

    [Fact]
    public async Task MasterImportPreview_ShouldStillAcceptExactly500MentorRows()
    {
        var workbook = CreateWorkbookWithLeftovers(sheet =>
        {
            for (var row = 4; row <= 501; row++)
            {
                sheet.Cell(row, 1).Value = row - 1;
                sheet.Cell(row, 2).Value = $"Bulk Mentor {Guid.NewGuid():N}";
            }
        });

        var (status, preview, body) = await PreviewWorkbookAsync(workbook);

        status.Should().Be(HttpStatusCode.OK, body);
        preview!.TotalRows.Should().Be(501, "500 enterprise mentors and one lecturer");
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
