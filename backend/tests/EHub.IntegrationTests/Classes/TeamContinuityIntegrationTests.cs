using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Classes.ImportStudents;
using EHub.Application.Features.Teams.Continuations;
using EHub.Application.Features.Teams.Lineage;
using EHub.Application.Features.Teams.ManageTeams;
using EHub.Contracts.Classes;
using EHub.Contracts.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EHub.IntegrationTests.Classes;

[Collection("Sequential")]
public sealed class TeamContinuityIntegrationTests
{
    private static int _yearCounter = 3000;

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TeamContinuityIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private sealed record World(
        Guid AdminId,
        User NewLecturer,
        User OldLecturer,
        User LeaderUser,
        User LateMemberUser,
        User OutsiderUser,
        Class OldClass,
        Class NewClass,
        Team OldTeam,
        Project OldProject,
        Guid OldSubmissionId,
        Student[] Students);

    [Fact]
    public async Task Import_ContinuesEligibleTeamWithSameLineageAndLeavesOldSemesterUntouched()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var world = await SeedWorldAsync(context);

        var (preview, commit) = await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[..5]);

        preview.Continuation.Should().NotBeNull();
        preview.Continuation!.CreatedCount.Should().Be(1);
        commit.Continuation!.CreatedCount.Should().Be(1);
        commit.Continuation.Items.Single().Outcome.Should().Be(TeamContinuationOutcomes.Created);

        var newTeam = await context.Teams.AsNoTracking()
            .Include(team => team.TeamMembers)
            .Include(team => team.Project).ThenInclude(project => project!.ProjectTags)
            .SingleAsync(team => team.ClassId == world.NewClass.Id);
        newTeam.TeamLineageId.Should().Be(world.OldTeam.TeamLineageId);
        newTeam.PreviousTeamId.Should().Be(world.OldTeam.Id);
        newTeam.TeamName.Should().Be(world.OldTeam.TeamName);
        newTeam.TeamMembers.Should().HaveCount(5);
        newTeam.TeamMembers.Single(member => member.RoleInTeam == TeamMemberRole.Leader)
            .StudentId.Should().Be(world.Students[0].Id);

        newTeam.Project.Should().NotBeNull();
        newTeam.Project!.ProjectLineageId.Should().Be(world.OldProject.ProjectLineageId);
        newTeam.Project.PreviousProjectId.Should().Be(world.OldProject.Id);
        newTeam.Project.Name.Should().Be(world.OldProject.Name);
        newTeam.Project.Status.Should().Be(ProjectStatus.Approved);
        newTeam.Project.IsHighPotential.Should().BeTrue();
        newTeam.Project.ProjectTags.Select(tag => tag.TagName).Should().BeEquivalentTo(["fintech"]);
        (await context.Submissions.AsNoTracking().CountAsync(item => item.ProjectId == newTeam.Project.Id))
            .Should().Be(0);

        // Previous semester data is not modified, moved or scored again.
        var oldTeam = await context.Teams.AsNoTracking().Include(team => team.TeamMembers)
            .SingleAsync(team => team.Id == world.OldTeam.Id);
        oldTeam.ClassId.Should().Be(world.OldClass.Id);
        oldTeam.TeamMembers.Should().HaveCount(5).And.OnlyContain(member => member.CountsTowardActiveTeam);
        (await context.Submissions.AsNoTracking().CountAsync(item => item.TeamId == world.OldTeam.Id)).Should().Be(1);
        (await context.Evaluations.AsNoTracking().CountAsync(item => item.ProjectId == world.OldProject.Id)).Should().Be(1);

        var continuation = await context.TeamContinuations.AsNoTracking().SingleAsync(item => item.CreatedTeamId == newTeam.Id);
        continuation.Status.Should().Be(TeamContinuationStatus.Active);
        continuation.TargetClassId.Should().Be(world.NewClass.Id);
        (await context.ClassAuditLogs.AsNoTracking().AnyAsync(log =>
            log.ClassId == world.NewClass.Id && log.Action == "TEAM_CONTINUITY_APPLIED")).Should().BeTrue();
    }

    [Fact]
    public async Task RepeatedContinuation_DoesNotDuplicateTeamsProjectsOrContinuations()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var world = await SeedWorldAsync(context);
        await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[..5]);

        var service = new TeamContinuationService(context);
        var targetClass = await context.Classes.SingleAsync(item => item.Id == world.NewClass.Id);
        var again = await service.ApplyAsync(targetClass, world.AdminId);
        again.CreatedCount.Should().Be(0);
        again.MembersAddedCount.Should().Be(0);
        context.ChangeTracker.Clear();

        (await context.Teams.AsNoTracking().CountAsync(team => team.ClassId == world.NewClass.Id)).Should().Be(1);
        (await context.Projects.AsNoTracking().CountAsync(project => project.ProjectLineageId == world.OldProject.ProjectLineageId))
            .Should().Be(2);
        (await context.TeamContinuations.AsNoTracking()
            .CountAsync(item => item.TeamLineageId == world.OldTeam.TeamLineageId)).Should().Be(1);
    }

    [Fact]
    public async Task Import_WhenFormerLeaderIsAbsent_AssignsEarliestJoinedMemberAsLeader()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var world = await SeedWorldAsync(context);

        // Students[0] is the previous leader; Students[1] joined the previous team first among the rest.
        await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[1..5]);

        var members = await context.TeamMembers.AsNoTracking()
            .Where(member => member.ClassId == world.NewClass.Id)
            .ToListAsync();
        members.Should().HaveCount(4);
        members.Single(member => member.RoleInTeam == TeamMemberRole.Leader)
            .StudentId.Should().Be(world.Students[1].Id);
    }

    [Fact]
    public async Task Import_InBatches_WaitsUntilEligibleThenAddsLateMembersOnce()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var world = await SeedWorldAsync(context);

        var (_, first) = await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[..3]);
        first.Continuation!.CreatedCount.Should().Be(0);
        first.Continuation.Items.Single().Outcome.Should().Be(TeamContinuationOutcomes.NotEligible);
        first.Continuation.Items.Single().Reasons.Should().Contain(reason => reason.Contains("at least 4"));
        (await context.Teams.AsNoTracking().CountAsync(team => team.ClassId == world.NewClass.Id)).Should().Be(0);

        var (_, second) = await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[3..4]);
        second.Continuation!.CreatedCount.Should().Be(1, System.Text.Json.JsonSerializer.Serialize(second.Continuation));
        (await context.TeamMembers.AsNoTracking().CountAsync(member => member.ClassId == world.NewClass.Id)).Should().Be(4);

        var (_, third) = await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[4..5]);
        third.Continuation!.MembersAddedCount.Should().Be(1);
        (await context.TeamMembers.AsNoTracking().CountAsync(member => member.ClassId == world.NewClass.Id)).Should().Be(5);

        // A student removed by the lecturer is not added back by a later run.
        var removed = await context.TeamMembers.SingleAsync(member =>
            member.ClassId == world.NewClass.Id && member.StudentId == world.Students[4].Id);
        context.TeamMembers.Remove(removed);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var targetClass = await context.Classes.SingleAsync(item => item.Id == world.NewClass.Id);
        var rerun = await new TeamContinuationService(context).ApplyAsync(targetClass, world.AdminId);
        rerun.MembersAddedCount.Should().Be(0);
        context.ChangeTracker.Clear();
        (await context.TeamMembers.AsNoTracking().CountAsync(member => member.ClassId == world.NewClass.Id)).Should().Be(4);
    }

    [Fact]
    public async Task Import_WhenTeamLacksOneMajorGroup_ReportsReasonAndCreatesNothing()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var world = await SeedWorldAsync(context);

        // Students 1, 2 and 4 are GROUP_2; student 3 is GROUP_1, student 0 is GROUP_1. Drop both GROUP_1 members.
        var (_, commit) = await ImportAsync(
            context, world.NewClass.Id, world.AdminId, [world.Students[1], world.Students[2], world.Students[4], world.Students[5]]);

        commit.Continuation!.CreatedCount.Should().Be(0);
        commit.Continuation.NotEligibleCount.Should().Be(1);
        commit.Continuation.Items.Single().Reasons.Should().Contain(reason => reason.Contains("GROUP_1"));
        (await context.Teams.AsNoTracking().CountAsync(team => team.ClassId == world.NewClass.Id)).Should().Be(0);
    }

    [Fact]
    public async Task DeletingContinuedTeam_MarksItDissolved_AndLaterContinuationDoesNotRecreateIt()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var world = await SeedWorldAsync(context);
        await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[..5]);
        var newTeamId = (await context.Teams.AsNoTracking().SingleAsync(team => team.ClassId == world.NewClass.Id)).Id;

        var handler = new TeamManagementHandler(context, new UnitOfWork(context));
        var delete = await handler.DeleteAsync(newTeamId, world.NewLecturer.Id, SystemRoles.Lecturer);
        delete.IsSuccess.Should().BeTrue(delete.IsFailure ? delete.Error.Message : string.Empty);
        context.ChangeTracker.Clear();

        var continuation = await context.TeamContinuations.AsNoTracking()
            .SingleAsync(item => item.TeamLineageId == world.OldTeam.TeamLineageId);
        continuation.Status.Should().Be(TeamContinuationStatus.Dissolved);
        continuation.DissolvedByUserId.Should().Be(world.NewLecturer.Id);
        continuation.CreatedTeamId.Should().BeNull();

        var targetClass = await context.Classes.SingleAsync(item => item.Id == world.NewClass.Id);
        var rerun = await new TeamContinuationService(context).ApplyAsync(targetClass, world.AdminId);
        rerun.CreatedCount.Should().Be(0);
        rerun.Items.Single().Outcome.Should().Be(TeamContinuationOutcomes.Dissolved);
        context.ChangeTracker.Clear();

        (await context.Teams.AsNoTracking().CountAsync(team => team.ClassId == world.NewClass.Id)).Should().Be(0);
        (await context.Teams.AsNoTracking().AnyAsync(team => team.Id == world.OldTeam.Id)).Should().BeTrue();
        (await context.Submissions.AsNoTracking().CountAsync(item => item.TeamId == world.OldTeam.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Lineage_StudentsSeeOwnScoresOnlyAndLaterMembersSeeNoOldScores()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var world = await SeedWorldAsync(context);
        // Students[5] never belonged to the old term; joins the continued team through the normal team flow.
        await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[..5]);
        await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[5..]);
        var newTeam = await context.Teams.AsNoTracking().SingleAsync(team => team.ClassId == world.NewClass.Id);
        context.TeamMembers.Add(new TeamMember
        {
            TeamId = newTeam.Id,
            ClassId = world.NewClass.Id,
            StudentId = world.Students[5].Id,
            RoleInTeam = TeamMemberRole.Member,
            CountsTowardActiveTeam = true,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var handler = new TeamLineageHandler(context);

        var leader = await handler.GetLineageAsync(newTeam.Id, world.LeaderUser.Id, SystemRoles.Student);
        leader.IsSuccess.Should().BeTrue();
        leader.Value.Terms.Should().HaveCount(2);
        leader.Value.Terms.Should().OnlyContain(term => term.CanViewSubmissions && term.CanViewScores);

        var late = await handler.GetLineageAsync(newTeam.Id, world.LateMemberUser.Id, SystemRoles.Student);
        late.IsSuccess.Should().BeTrue();
        late.Value.Terms.Should().HaveCount(2);
        var oldTerm = late.Value.Terms.Single(term => term.TeamId == world.OldTeam.Id);
        oldTerm.CanViewSubmissions.Should().BeTrue();
        oldTerm.CanViewScores.Should().BeFalse();

        var lateSubmissions = await handler.GetTermSubmissionsAsync(
            newTeam.Id, world.OldTeam.Id, world.LateMemberUser.Id, SystemRoles.Student);
        lateSubmissions.IsSuccess.Should().BeTrue();
        lateSubmissions.Value.Single().Evaluations.Should().BeNull();

        var leaderSubmissions = await handler.GetTermSubmissionsAsync(
            newTeam.Id, world.OldTeam.Id, world.LeaderUser.Id, SystemRoles.Student);
        leaderSubmissions.Value.Single().Evaluations.Should().ContainSingle()
            .Which.TotalScore.Should().Be(8);

        // The new term has no submissions yet, and nothing was carried over.
        var newTermSubmissions = await handler.GetTermSubmissionsAsync(
            newTeam.Id, newTeam.Id, world.LeaderUser.Id, SystemRoles.Student);
        newTermSubmissions.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Lineage_DeniesOutsidersAndTermsOutsideTheLineage()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var world = await SeedWorldAsync(context);
        await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[..5]);
        var newTeam = await context.Teams.AsNoTracking().SingleAsync(team => team.ClassId == world.NewClass.Id);
        var otherLecturer = await CreateUserAsync(context, SystemRoles.Lecturer, "other-lecturer");
        var handler = new TeamLineageHandler(context);

        var outsider = await handler.GetLineageAsync(newTeam.Id, world.OutsiderUser.Id, SystemRoles.Student);
        outsider.IsFailure.Should().BeTrue();
        outsider.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);

        var unrelatedLecturer = await handler.GetLineageAsync(newTeam.Id, otherLecturer.Id, SystemRoles.Lecturer);
        unrelatedLecturer.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);

        var missing = await handler.GetLineageAsync(Guid.NewGuid(), world.LeaderUser.Id, SystemRoles.Student);
        missing.Error.Code.Should().Be(ErrorCodes.TeamNotFound);

        // A team from another lineage cannot be reached by passing its id as a term.
        var foreignTeam = await SeedUnrelatedTeamAsync(context, world);
        var foreign = await handler.GetTermSubmissionsAsync(
            newTeam.Id, foreignTeam.Id, world.LeaderUser.Id, SystemRoles.Student);
        foreign.IsFailure.Should().BeTrue();
        foreign.Error.Code.Should().Be(ErrorCodes.TeamNotFound);

        // The previous-semester lecturer only sees terms up to their own class, not the newer one.
        var oldLecturer = await handler.GetLineageAsync(world.OldTeam.Id, world.OldLecturer.Id, SystemRoles.Lecturer);
        oldLecturer.IsSuccess.Should().BeTrue();
        oldLecturer.Value.Terms.Select(term => term.TeamId).Should().BeEquivalentTo([world.OldTeam.Id]);

        // The new lecturer reads the earlier term (read-only) including scores.
        var newLecturer = await handler.GetLineageAsync(newTeam.Id, world.NewLecturer.Id, SystemRoles.Lecturer);
        newLecturer.Value.Terms.Should().HaveCount(2).And.OnlyContain(term => term.CanViewScores);
    }

    [Fact]
    public async Task LineageEndpoints_RequireAuthenticationAndAdminOnlyReport()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var world = await SeedWorldAsync(context);
        await ImportAsync(context, world.NewClass.Id, world.AdminId, world.Students[..5]);
        var newTeam = await context.Teams.AsNoTracking().SingleAsync(team => team.ClassId == world.NewClass.Id);
        var admin = await context.Users.AsNoTracking().SingleAsync(user => user.Id == world.AdminId);

        (await _client.GetAsync($"/api/teams/{newTeam.Id}/lineage")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync($"/api/teams/{newTeam.Id}/lineage/{world.OldTeam.Id}/submissions"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.GetAsync($"/api/admin/team-continuity?semesterId={world.NewClass.SemesterId}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var studentToken = GenerateToken(scope.ServiceProvider, world.LeaderUser, SystemRoles.Student);
        var forbidden = await _client.SendAsync(Authorized(
            $"/api/admin/team-continuity?semesterId={world.NewClass.SemesterId}", studentToken));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var outsiderToken = GenerateToken(scope.ServiceProvider, world.OutsiderUser, SystemRoles.Student);
        var denied = await _client.SendAsync(Authorized($"/api/teams/{newTeam.Id}/lineage", outsiderToken));
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var leaderToken = GenerateToken(scope.ServiceProvider, world.LeaderUser, SystemRoles.Student);
        var ok = await _client.SendAsync(Authorized($"/api/teams/{newTeam.Id}/lineage", leaderToken));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var lineage = await ok.Content.ReadFromJsonAsync<ApiResponse<TeamLineageDto>>();
        lineage!.Data!.Terms.Should().HaveCount(2, System.Text.Json.JsonSerializer.Serialize(lineage.Data));

        var adminToken = GenerateToken(scope.ServiceProvider, admin, SystemRoles.Admin);
        var report = await _client.SendAsync(Authorized(
            $"/api/admin/team-continuity?semesterId={world.NewClass.SemesterId}", adminToken));
        report.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await report.Content.ReadFromJsonAsync<ApiResponse<TeamContinuityReportDto>>();
        body!.Data!.ContinuedTeamCount.Should().Be(1);
        body.Data.ContinuedProjectCount.Should().Be(1);
        body.Data.DissolvedCount.Should().Be(0);
    }

    // ---- seeding helpers -------------------------------------------------------------------

    private static async Task<World> SeedWorldAsync(AppDbContext context)
    {
        var admin = await context.Users
            .Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .FirstAsync(user => user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Admin));
        var newLecturer = await CreateUserAsync(context, SystemRoles.Lecturer, "new-lecturer");
        var oldLecturer = await CreateUserAsync(context, SystemRoles.Lecturer, "old-lecturer");
        var leaderUser = await CreateUserAsync(context, SystemRoles.Student, "leader");
        var lateUser = await CreateUserAsync(context, SystemRoles.Student, "late-member");
        var outsiderUser = await CreateUserAsync(context, SystemRoles.Student, "outsider");

        var year = Interlocked.Increment(ref _yearCounter);
        var oldSemester = new Semester
        {
            Code = $"T{year}A",
            Name = $"Continuity old {year}",
            Term = SemesterTerm.Spring,
            Year = year,
            StartDate = new DateOnly(year, 1, 1),
            EndDate = new DateOnly(year, 5, 1),
            Status = SemesterStatus.Planned,
            CreatedBy = admin.Id
        };
        var newSemester = new Semester
        {
            Code = $"T{year}B",
            Name = $"Continuity new {year}",
            Term = SemesterTerm.Fall,
            Year = year,
            StartDate = new DateOnly(year, 9, 1),
            EndDate = new DateOnly(year, 12, 20),
            Status = SemesterStatus.Planned,
            CreatedBy = admin.Id
        };
        var unique = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var oldCourse = new Course { Code = $"O{unique}", Name = $"Old course {unique}", Status = CourseStatus.Active, CreatedBy = admin.Id };
        var newCourse = new Course { Code = $"N{unique}", Name = $"New course {unique}", Status = CourseStatus.Active, CreatedBy = admin.Id };
        const string scheduleJson = "[{\"dayOfWeek\":1,\"slotNumber\":1,\"room\":\"C-101\"}]";
        var oldClass = new Class
        {
            ClassCode = $"{oldCourse.Code}_1",
            Slug = $"{oldSemester.Code}-{oldCourse.Code}-1".ToLowerInvariant(),
            ClassIndex = 1,
            CourseId = oldCourse.Id,
            SemesterId = oldSemester.Id,
            PrimaryLecturerId = oldLecturer.Id,
            ScheduleJson = scheduleJson,
            Status = ClassStatus.Active,
            CreatedById = admin.Id,
            CreatedBy = admin.Id
        };
        var newClass = new Class
        {
            ClassCode = $"{newCourse.Code}_1",
            Slug = $"{newSemester.Code}-{newCourse.Code}-1".ToLowerInvariant(),
            ClassIndex = 1,
            CourseId = newCourse.Id,
            SemesterId = newSemester.Id,
            PrimaryLecturerId = newLecturer.Id,
            ScheduleJson = scheduleJson,
            Status = ClassStatus.Active,
            CreatedById = admin.Id,
            CreatedBy = admin.Id
        };
        context.Semesters.AddRange(oldSemester, newSemester);
        context.Courses.AddRange(oldCourse, newCourse);
        context.Classes.AddRange(oldClass, newClass);
        context.ClassLecturers.AddRange(
            new ClassLecturer { ClassId = oldClass.Id, LecturerId = oldLecturer.Id, IsPrimary = true, AssignedById = admin.Id },
            new ClassLecturer { ClassId = newClass.Id, LecturerId = newLecturer.Id, IsPrimary = true, AssignedById = admin.Id });

        // 0 leader GROUP_1, 1 GROUP_2, 2 GROUP_2, 3 GROUP_1, 4 GROUP_2, 5 new GROUP_2 member
        var majors = new[] { MajorCodes.BBA_MKT, MajorCodes.BIT_SE, MajorCodes.BIT_SE, MajorCodes.BBA_FIN, MajorCodes.BIT_AI, MajorCodes.BIT_SE };
        var students = majors.Select((major, index) =>
        {
            var roll = $"CT{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            return new Student
            {
                RollNumber = roll,
                NormalizedRollNumber = roll,
                FullName = $"Continuity Student {index}",
                Email = $"continuity-{Guid.NewGuid():N}@example.com",
                MajorCode = major,
                Status = StudentStatus.Active,
                CreatedBy = admin.Id
            };
        }).ToArray();
        // The import rejects a registered student whose email differs from the account email.
        students[0].UserId = leaderUser.Id;
        students[0].Email = leaderUser.Email;
        students[5].UserId = lateUser.Id;
        students[5].Email = lateUser.Email;
        var outsiderRoll = $"CT{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var outsiderStudent = new Student
        {
            RollNumber = outsiderRoll,
            NormalizedRollNumber = outsiderRoll,
            FullName = "Continuity Outsider",
            Email = outsiderUser.Email,
            MajorCode = MajorCodes.BIT_SE,
            Status = StudentStatus.Active,
            UserId = outsiderUser.Id,
            CreatedBy = admin.Id
        };
        context.Students.AddRange(students);
        context.Students.Add(outsiderStudent);
        await context.SaveChangesAsync();

        var joinedBase = DateTime.UtcNow.AddDays(-200);
        var team = new Team
        {
            ClassId = oldClass.Id,
            TeamCode = $"{oldClass.ClassCode}_TEAM_1",
            TeamName = $"Alpha {unique}",
            Description = "Alpha team",
            Status = TeamStatus.Active,
            CreatedById = admin.Id
        };
        for (var index = 0; index < 5; index++)
        {
            var enrollment = new ClassStudent
            {
                ClassId = oldClass.Id,
                StudentId = students[index].Id,
                SemesterId = oldSemester.Id,
                CourseId = oldCourse.Id,
                EnrollmentStatus = EnrollmentStatus.Active,
                CountsTowardCourseSemesterLimit = true,
                MajorCodeAtEnrollment = majors[index]
            };
            context.ClassStudents.Add(enrollment);
            team.TeamMembers.Add(new TeamMember
            {
                TeamId = team.Id,
                ClassId = oldClass.Id,
                StudentId = students[index].Id,
                RoleInTeam = index == 0 ? TeamMemberRole.Leader : TeamMemberRole.Member,
                CountsTowardActiveTeam = true,
                JoinedAt = joinedBase.AddHours(index)
            });
        }

        var project = new Project
        {
            TeamId = team.Id,
            Name = $"Wallet {unique}",
            Description = "A student wallet",
            Problem = "Students lose track of money",
            Status = ProjectStatus.Approved,
            IsHighPotential = true,
            CreatedById = admin.Id,
            SubmittedAt = DateTime.UtcNow.AddDays(-150)
        };
        project.ProjectTags.Add(new ProjectTag
        {
            ProjectId = project.Id,
            TagName = "fintech",
            NormalizedTagName = "fintech",
            TagType = ProjectTagType.Keyword
        });
        team.Project = project;
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var checkpoint = new Checkpoint
        {
            ClassId = oldClass.Id,
            Name = "Checkpoint 1",
            CheckpointNumber = 1,
            CourseWeight = 10,
            Status = CheckpointStatus.Open,
            CreatedById = admin.Id
        };
        var rubric = new Rubric { Name = $"Rubric {unique}", ClassId = oldClass.Id, CourseWeight = 10, CreatedById = admin.Id };
        var submission = new Submission
        {
            ProjectId = project.Id,
            TeamId = team.Id,
            CheckpointId = checkpoint.Id,
            Title = "Old semester submission",
            Status = SubmissionStatus.Submitted,
            SubmittedAt = DateTime.UtcNow.AddDays(-100)
        };
        context.Checkpoints.Add(checkpoint);
        context.Rubrics.Add(rubric);
        context.Submissions.Add(submission);
        await context.SaveChangesAsync();
        context.Evaluations.Add(new Evaluation
        {
            ProjectId = project.Id,
            SubmissionId = submission.Id,
            RubricId = rubric.Id,
            EvaluatorId = oldLecturer.Id,
            EvaluatorRole = EvaluatorRole.Lecturer,
            TotalScore = 8,
            MaxTotalScore = 10,
            Status = EvaluationStatus.Published,
            PublishedAt = DateTime.UtcNow.AddDays(-90)
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new World(
            admin.Id, newLecturer, oldLecturer, leaderUser, lateUser, outsiderUser,
            oldClass, newClass, team, project, submission.Id, students);
    }

    private static async Task<Team> SeedUnrelatedTeamAsync(AppDbContext context, World world)
    {
        var team = new Team
        {
            ClassId = world.OldClass.Id,
            TeamCode = $"{world.OldClass.ClassCode}_TEAM_9",
            TeamName = $"Unrelated {Guid.NewGuid():N}"[..20],
            Status = TeamStatus.Active
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return team;
    }

    private static async Task<User> CreateUserAsync(AppDbContext context, string roleName, string suffix)
    {
        var role = await context.Roles.SingleAsync(item => item.Name == roleName);
        var email = $"continuity-{suffix}-{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            FullName = $"Continuity {suffix}",
            Email = email,
            NormalizedEmail = email.ToLowerInvariant(),
            PasswordHash = "integration-test-only",
            Status = UserStatus.Active,
            IsEmailVerified = true
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role });
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return user;
    }

    private static async Task<(ImportStudentsPreviewResponse Preview, ImportStudentsCommitResponse Commit)> ImportAsync(
        AppDbContext context, Guid classId, Guid adminId, Student[] students)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        var headers = new[] { "RollNumber", "Fullname", "Email", "MajorCode" };
        for (var column = 0; column < headers.Length; column++) worksheet.Cell(1, column + 1).Value = headers[column];
        for (var index = 0; index < students.Length; index++)
        {
            worksheet.Cell(index + 2, 1).Value = students[index].NormalizedRollNumber;
            worksheet.Cell(index + 2, 2).Value = students[index].FullName;
            worksheet.Cell(index + 2, 3).Value = students[index].Email;
            worksheet.Cell(index + 2, 4).Value = students[index].MajorCode;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "roster.xlsx");

        var preview = await new PreviewImportStudentsCommandHandler(context)
            .HandleAsync(classId, file, adminId, SystemRoles.Admin);
        preview.IsSuccess.Should().BeTrue(preview.IsFailure ? preview.Error.Message : string.Empty);
        preview.Value.ErrorRowsCount.Should().Be(0, string.Join("; ", preview.Value.Rows.Where(row => !row.IsValid).Select(row => row.ErrorMessage ?? row.MajorWarningMessage)));
        context.ChangeTracker.Clear();

        var commit = await new CommitImportStudentsCommandHandler(context, new UnitOfWork(context))
            .HandleAsync(
                classId,
                new CommitImportStudentsRequest { SessionId = preview.Value.SessionId },
                adminId,
                SystemRoles.Admin);
        commit.IsSuccess.Should().BeTrue(commit.IsFailure ? commit.Error.Message : string.Empty);
        context.ChangeTracker.Clear();
        commit.Value.ErrorCount.Should().Be(0, string.Join("; ", commit.Value.Errors.Select(error => $"{error.StudentCode}: {error.ErrorMessage}")));
        return (preview.Value, commit.Value);
    }

    private static string GenerateToken(IServiceProvider services, User user, string role) =>
        services.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [role]).Token;

    private static HttpRequestMessage Authorized(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
