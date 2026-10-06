using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Classes;

public sealed partial class TeamWorkflowIntegrationTests
{
    [Fact]
    public async Task StudentDirectoryAudit_LecturerCannotReadStudentsOutsideAssignedClasses()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var outsider = await CreateUserAsync(context, SystemRoles.Lecturer, "directory-outsider");
        using var client = _factory.CreateClient();
        var url = $"/api/users/{seed.ProposerUserId}";
        using var anonymous = await client.GetAsync(url);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var student = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.ProposerUserId, SystemRoles.Student);
        student.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var forbidden = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, outsider.Id, SystemRoles.Lecturer);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var allowed = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.LecturerId, SystemRoles.Lecturer);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await context.Users.SingleAsync(item => item.Id == seed.ProposerUserId);
        using var list = await SendAuditGetAsync(client, scope.ServiceProvider, context,
            $"/api/users?role=STUDENT&search={Uri.EscapeDataString(user.Email)}", outsider.Id, SystemRoles.Lecturer);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        using var listDocument = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        listDocument.RootElement.GetProperty("data").GetProperty("users").GetArrayLength().Should().Be(0);
        using var roster = await SendAuditGetAsync(client, scope.ServiceProvider, context,
            $"/api/classes/{seed.ClassId}/students", outsider.Id, SystemRoles.Lecturer);
        roster.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RankingAudit_PublishedTiesReceiveTheSameRank()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var first = await SeedAuditEvaluationAsync(context, seed);
        var second = new Evaluation
        {
            Project = new Project { TeamId = seed.OtherTeamId!.Value, Name = "Second audit project", CreatedById = seed.AdminId },
            RubricId = first.RubricId, EvaluatorId = seed.LecturerId,
            TotalScore = first.TotalScore, MaxTotalScore = 10, Status = EvaluationStatus.Published
        };
        context.Evaluations.Add(second);
        await context.SaveChangesAsync();
        using var client = _factory.CreateClient();
        var url = $"/api/rankings?classId={seed.ClassId}&checkpointNumber=1";
        using var tied = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.AdminId, SystemRoles.Admin);
        tied.StatusCode.Should().Be(HttpStatusCode.OK);
        using var tiedDocument = JsonDocument.Parse(await tied.Content.ReadAsStringAsync());
        tiedDocument.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Should().OnlyContain(item => item.GetProperty("rank").GetInt32() == 1);
        second.TotalScore = 7;
        await context.SaveChangesAsync();
        using var ranked = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.LecturerId, SystemRoles.Lecturer);
        using var rankedDocument = JsonDocument.Parse(await ranked.Content.ReadAsStringAsync());
        var items = rankedDocument.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToArray();
        items.Single(item => item.GetProperty("teamId").GetGuid() == seed.TeamId).GetProperty("rank").GetInt32().Should().Be(1);
        items.Single(item => item.GetProperty("teamId").GetGuid() == seed.OtherTeamId).GetProperty("rank").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task RankingAudit_RanksPublishedTeamsOnly_AndNeverSerializesScores()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var evaluation = await SeedAuditEvaluationAsync(context, seed);
        using var client = _factory.CreateClient();
        var url = $"/api/rankings?classId={seed.ClassId}&checkpointNumber=1";
        using var response = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.LecturerId, SystemRoles.Lecturer);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("\"score\":").And.NotContain("\"courseTotal\":").And.NotContain("memberScores");
        using var document = JsonDocument.Parse(json);
        var items = document.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToArray();
        items.Single(item => item.GetProperty("teamId").GetGuid() == seed.TeamId).GetProperty("rank").GetInt32().Should().Be(1);
        items.Single(item => item.GetProperty("teamId").GetGuid() == seed.OtherTeamId).GetProperty("rank").ValueKind.Should().Be(JsonValueKind.Null);

        evaluation.Status = EvaluationStatus.Submitted;
        await context.SaveChangesAsync();
        using var hidden = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.LecturerId, SystemRoles.Lecturer);
        using var hiddenDocument = JsonDocument.Parse(await hidden.Content.ReadAsStringAsync());
        hiddenDocument.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Should().OnlyContain(item => item.GetProperty("rank").ValueKind == JsonValueKind.Null);
        using var invalid = await SendAuditGetAsync(client, scope.ServiceProvider, context,
            "/api/rankings?checkpointNumber=0", seed.LecturerId, SystemRoles.Lecturer);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var student = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.ProposerUserId, SystemRoles.Student);
        student.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var mentor = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.MentorUserId, SystemRoles.Mentor);
        mentor.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var outsider = await CreateUserAsync(context, SystemRoles.Lecturer, "ranking-outsider");
        using var denied = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, outsider.Id, SystemRoles.Lecturer);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The assignment table and primary lecturer are both valid read scopes.
        var assignment = await context.ClassLecturers.SingleAsync(item => item.ClassId == seed.ClassId);
        context.ClassLecturers.Remove(assignment);
        await context.SaveChangesAsync();
        context.ClassLecturers.Add(new ClassLecturer { ClassId = seed.ClassId, LecturerId = outsider.Id,
            IsPrimary = true, AssignedById = seed.AdminId });
        await context.SaveChangesAsync();
        using var assigned = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, outsider.Id, SystemRoles.Lecturer);
        assigned.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EvaluationAudit_StudentGetsOnlyOwnPublishedScore_MentorGetsNoScores()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var evaluation = await SeedAuditEvaluationAsync(context, seed);
        using var client = _factory.CreateClient();
        var url = $"/api/workspace/checkpoints/teams/{seed.TeamId}/checkpoints/1/evaluation-summary";
        using var anonymous = await client.GetAsync(url);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var response = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.ProposerUserId, SystemRoles.Student);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("checkpointTotal").And.NotContain("averageScore");
        foreach (var peerId in seed.StudentIds.Skip(1)) json.Should().NotContain(peerId.ToString());
        using var document = JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        data.GetProperty("history").GetArrayLength().Should().Be(0);
        var ownScores = data.GetProperty("evaluations")[0].GetProperty("memberScores");
        ownScores.GetArrayLength().Should().Be(1);
        ownScores[0].GetProperty("score").GetDecimal().Should().Be(6);
        using var mentor = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.MentorUserId, SystemRoles.Mentor);
        mentor.StatusCode.Should().Be(HttpStatusCode.OK);
        var mentorJson = await mentor.Content.ReadAsStringAsync();
        mentorJson.Should().NotContain("\"score\":").And.NotContain("checkpointTotal").And.NotContain("averageScore");
        using var otherTeam = await SendAuditGetAsync(client, scope.ServiceProvider, context,
            $"/api/workspace/checkpoints/teams/{seed.OtherTeamId}/checkpoints/1/evaluation-summary", seed.ProposerUserId, SystemRoles.Student);
        otherTeam.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        evaluation.Status = EvaluationStatus.Submitted;
        await context.SaveChangesAsync();
        using var hidden = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.ProposerUserId, SystemRoles.Student);
        (await hidden.Content.ReadAsStringAsync()).Should().NotContain("\"score\":");
    }

    [Fact]
    public async Task PreviousScoresAudit_UsesCurrentEnrollment_AndRevokesAccessAfterDrop()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var evaluation = await SeedAuditEvaluationAsync(context, seed);
        var currentClass = await context.Classes.Include(item => item.Semester).SingleAsync(item => item.Id == seed.ClassId);
        var semesters = await context.Semesters.Where(item => item.Status == SemesterStatus.Completed || item.Status == SemesterStatus.Archived).ToArrayAsync();
        var previous = semesters.Where(item => item.Year < currentClass.Semester.Year ||
            item.Year == currentClass.Semester.Year && item.Term < currentClass.Semester.Term)
            .OrderByDescending(item => item.Year).ThenByDescending(item => item.Term).FirstOrDefault();
        if (previous is null)
        {
            var term = currentClass.Semester.Term == SemesterTerm.Spring ? SemesterTerm.Fall : currentClass.Semester.Term - 1;
            var year = currentClass.Semester.Term == SemesterTerm.Spring ? currentClass.Semester.Year - 1 : currentClass.Semester.Year;
            previous = await context.Semesters.FirstOrDefaultAsync(item => item.Term == term && item.Year == year)
                ?? new Semester { Code = $"AUDIT-{year}-{term}", Name = "Audit previous semester", Year = year, Term = term };
            if (context.Entry(previous).State == EntityState.Detached) context.Semesters.Add(previous);
            previous.Status = SemesterStatus.Completed;
            previous.CompletedAtUtc = DateTime.UtcNow;
            previous.CompletionReason = "Integration fixture";
        }
        var oldClass = new Class
        {
            ClassCode = $"AUDIT-{Guid.NewGuid():N}"[..25], Slug = $"audit-{Guid.NewGuid():N}", ClassIndex = 1,
            CourseId = currentClass.CourseId, Semester = previous, Status = ClassStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow, CompletionReason = "Integration fixture", CreatedById = seed.AdminId
        };
        context.Classes.Add(oldClass);
        context.ClassStudents.Add(new ClassStudent
        {
            Class = oldClass, StudentId = seed.StudentIds[0], SemesterId = previous.Id, CourseId = currentClass.CourseId,
            EnrollmentStatus = EnrollmentStatus.Completed, CompletedAtUtc = DateTime.UtcNow,
            CountsTowardCourseSemesterLimit = true, MajorCodeAtEnrollment = "BIT_SE"
        });
        var oldTeam = new Team { Class = oldClass, TeamCode = $"AUDIT-{Guid.NewGuid():N}"[..25], TeamName = "Old private team", Status = TeamStatus.Active, CreatedById = seed.AdminId };
        oldTeam.TeamMembers.Add(new TeamMember { ClassId = oldClass.Id, StudentId = seed.StudentIds[0], CountsTowardActiveTeam = true, CreatedById = seed.AdminId });
        context.Teams.Add(oldTeam);
        var project = new Project { Team = oldTeam, Name = "Old private project", CreatedById = seed.AdminId };
        context.Projects.Add(project);
        var oldEvaluation = new Evaluation { Project = project, RubricId = evaluation.RubricId, EvaluatorId = seed.LecturerId,
            TotalScore = 9, MaxTotalScore = 10, Status = EvaluationStatus.Published, PublishedAt = DateTime.UtcNow };
        oldEvaluation.MemberScores.Add(new EvaluationMemberScore { StudentId = seed.StudentIds[0], Score = 5 });
        context.Evaluations.Add(oldEvaluation);
        await context.SaveChangesAsync();
        using var client = _factory.CreateClient();
        var url = $"/api/workspace/checkpoints/classes/{seed.ClassId}/students/{seed.StudentIds[0]}/previous-scores";
        using var anonymous = await client.GetAsync(url);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var student = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.ProposerUserId, SystemRoles.Student);
        student.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var outsider = await CreateUserAsync(context, SystemRoles.Lecturer, "historical-outsider");
        using var forbidden = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, outsider.Id, SystemRoles.Lecturer);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var allowed = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.LecturerId, SystemRoles.Lecturer);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await allowed.Content.ReadAsStringAsync();
        json.Should().NotContain("teamId").And.NotContain("email").And.NotContain("Old private");
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("data").GetProperty("components")[0].GetProperty("score").GetDecimal().Should().Be(5);
        oldEvaluation.Status = EvaluationStatus.Submitted;
        await context.SaveChangesAsync();
        using var hidden = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.LecturerId, SystemRoles.Lecturer);
        using var hiddenDocument = JsonDocument.Parse(await hidden.Content.ReadAsStringAsync());
        hiddenDocument.RootElement.GetProperty("data").GetProperty("components").GetArrayLength().Should().Be(0);
        var enrollment = await context.ClassStudents.SingleAsync(item => item.ClassId == seed.ClassId && item.StudentId == seed.StudentIds[0]);
        enrollment.EnrollmentStatus = EnrollmentStatus.Dropped;
        enrollment.CountsTowardCourseSemesterLimit = false;
        await context.SaveChangesAsync();
        using var revoked = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.LecturerId, SystemRoles.Lecturer);
        revoked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StudentInformationAudit_RedactsPeerPii_AndChecksChatResourceAccess()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, false, true);
        var group = new ChatGroup { ClassId = seed.ClassId, GroupName = "Private audit group", CreatedById = seed.AdminId };
        foreach (var id in seed.StudentIds) group.Members.Add(new ChatGroupMember { StudentId = id });
        context.ChatGroups.Add(group);
        await context.SaveChangesAsync();
        using var client = _factory.CreateClient();
        var url = $"/api/chat/groups/{group.Id}/members";
        using var anonymous = await client.GetAsync(url);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var outsider = await CreateUserAsync(context, SystemRoles.Student, "chat-outsider");
        using var forbidden = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, outsider.Id, SystemRoles.Student);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var messages = await SendAuditGetAsync(client, scope.ServiceProvider, context,
            $"/api/chat/groups/{group.Id}/messages", outsider.Id, SystemRoles.Student);
        messages.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var member = await SendAuditGetAsync(client, scope.ServiceProvider, context, url, seed.ProposerUserId, SystemRoles.Student);
        member.StatusCode.Should().Be(HttpStatusCode.OK);
        var peers = await context.Students.Where(item => seed.StudentIds.Skip(1).Contains(item.Id)).ToArrayAsync();
        var json = await member.Content.ReadAsStringAsync();
        foreach (var peer in peers)
        {
            json.Should().NotContain(peer.Email!).And.NotContain(peer.UserId!.Value.ToString());
        }
        using var classDetail = await SendAuditGetAsync(client, scope.ServiceProvider, context,
            $"/api/classes/my-class-detail/{seed.ClassId}", seed.ProposerUserId, SystemRoles.Student);
        classDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        var classJson = await classDetail.Content.ReadAsStringAsync();
        foreach (var peer in peers) classJson.Should().NotContain(peer.Email!).And.NotContain(peer.UserId!.Value.ToString());

        // The Verified/Unverified label is shared with classmates, but their majors and profiles stay private.
        var peerIds = peers.Select(peer => peer.Id).ToArray();
        var peerEnrollments = await context.ClassStudents
            .Where(item => item.ClassId == seed.ClassId && peerIds.Contains(item.StudentId)).ToArrayAsync();
        peerEnrollments.Should().NotBeEmpty();
        foreach (var enrollment in peerEnrollments) enrollment.MajorVerificationStatus = EnrollmentMajorVerificationStatus.Matched;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        using var verifiedDetail = await SendAuditGetAsync(client, scope.ServiceProvider, context,
            $"/api/classes/my-class-detail/{seed.ClassId}", seed.ProposerUserId, SystemRoles.Student);
        var detail = await verifiedDetail.Content.ReadFromJsonAsync<ApiResponse<StudentClassDetailResponse>>();
        var classmates = detail!.Data!.Students.Where(member => peerIds.Contains(member.StudentId)).ToArray();
        classmates.Should().HaveCount(peerIds.Length);
        classmates.Should().OnlyContain(member => member.MajorVerificationStatus == nameof(EnrollmentMajorVerificationStatus.Matched));
        classmates.Should().OnlyContain(member => member.Email == null && member.UserId == null
            && member.ProfileMajorCode == null && member.EnrollmentMajorCode == string.Empty);
    }

    private static async Task<Evaluation> SeedAuditEvaluationAsync(AppDbContext context, WorkflowSeed seed)
    {
        var courseId = await context.Classes.Where(item => item.Id == seed.ClassId).Select(item => item.CourseId).SingleAsync();
        var checkpoint = new Checkpoint { CourseId = courseId, Name = "Audit checkpoint", CheckpointNumber = 1,
            CourseWeight = 100, Status = CheckpointStatus.Open, CreatedById = seed.AdminId };
        var criterion = new RubricCriterion { Key = "audit", Name = "Audit criterion", Weight = 100, MaxScore = 10 };
        var rubric = new Rubric { CourseId = courseId, Checkpoint = checkpoint, Name = "Audit rubric", TotalWeight = 100,
            CourseWeight = 100, Status = RubricStatus.Active, CreatedById = seed.AdminId, Criteria = [criterion] };
        var project = new Project { TeamId = seed.TeamId!.Value, Name = "Audit project", CreatedById = seed.AdminId };
        var evaluation = new Evaluation { Project = project, Rubric = rubric, EvaluatorId = seed.LecturerId,
            TotalScore = 8, MaxTotalScore = 10, Status = EvaluationStatus.Published, PublishedAt = DateTime.UtcNow };
        evaluation.Details.Add(new EvaluationDetail { RubricCriterion = criterion, Score = 8 });
        evaluation.MemberScores.Add(new EvaluationMemberScore { StudentId = seed.StudentIds[0], Score = 6 });
        context.Evaluations.Add(evaluation);
        await context.SaveChangesAsync();
        return evaluation;
    }

    private static async Task<HttpResponseMessage> SendAuditGetAsync(HttpClient client, IServiceProvider services,
        AppDbContext context, string url, Guid userId, string role)
    {
        var user = await context.Users.AsNoTracking().SingleAsync(item => item.Id == userId);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            services.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [role]).Token);
        return await client.SendAsync(request);
    }
}
