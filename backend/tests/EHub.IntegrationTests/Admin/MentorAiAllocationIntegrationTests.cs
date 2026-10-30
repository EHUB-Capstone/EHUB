using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MentorProfileResponse = EHub.Contracts.Mentoring.MentorProfileResponse;

namespace EHub.IntegrationTests.Admin;

[Collection("Sequential")]
public sealed class MentorAiAllocationIntegrationTests(CustomWebApplicationFactory factory)
{
    private const string PreviewUrl = "/api/admin/mentors/allocations/preview";
    private const string CommitUrl = "/api/admin/mentors/allocations/commit";
    private const string PrivateMarker = "PRIVATE_PREVIOUS_TEAM_NOTES";

    [Fact]
    public async Task ClassPreviewUsesCompletedHistoryAndOnlyAcceptanceSavesAssignmentsAndOutbox()
    {
        using var app = App();
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);

        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        {
            SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai, Seed = 23
        });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain(PrivateMarker);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        preview.Strategy.Should().Be(MentorAllocationStrategies.Ai);
        preview.TeamCount.Should().Be(1);
        preview.CanCommit.Should().BeTrue();
        preview.Unfilled.Should().BeEmpty();
        preview.Assignments.Should().HaveCount(2).And.OnlyContain(row => row.TeamId == fixture.TeamId && row.ClassId == fixture.ClassId);
        preview.Assignments.Should().OnlyContain(row => row.FitScore > 0 && row.Reasons.Count > 0);
        var enterprise = preview.Assignments.Single(row => row.MentorType == nameof(MentorType.Enterprise));
        enterprise.MentorProfileId.Should().Be(fixture.EnterpriseProfileId);
        enterprise.Reasons.Should().Contain(reason => reason.Contains("Lịch sử đã ghi nhận"));
        enterprise.Reasons.Should().Contain(reason => reason.Contains("thành tích"));
        enterprise.History.Should().NotBeNull();
        enterprise.History!.CompletedSessionCount.Should().Be(1);
        enterprise.History.MentoredTeamCount.Should().Be(1);
        enterprise.History.AchievementCount.Should().Be(1);
        enterprise.History.HistoryBonus.Should().Be(1);
        enterprise.History.AchievementBonus.Should().Be(2);
        enterprise.FitScore.Should().Be(Math.Min(100, enterprise.History.BaseFitScore + 3));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MentorAssignments.CountAsync(row => row.TeamId == fixture.TeamId || row.TeamId == fixture.SiblingTeamId)).Should().Be(0);
            (await db.OutboxMessages.CountAsync(row => row.AggregateId == fixture.ClassId || row.AggregateId == fixture.SiblingClassId)).Should().Be(0);
        }

        using var commit = Authorized(HttpMethod.Post, CommitUrl, admin.Token,
            new CommitMentorAllocationRequest { SessionId = preview.SessionId });
        using var accepted = await client.SendAsync(commit);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await accepted.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationCommitResponse>>())!.Data!.CreatedCount.Should().Be(2);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.MentorAssignments.AsNoTracking().Where(row => row.TeamId == fixture.TeamId).ToArrayAsync();
            saved.Should().HaveCount(2).And.OnlyContain(row => row.Status == MentorAssignmentStatus.Active && row.EndedAt == null);
            saved.Should().OnlyContain(row => row.ProjectId == fixture.TargetProjectId,
                "future project achievements must stay linked to the mentors who were assigned");
            saved.Select(row => row.Slot).Should().BeEquivalentTo([MentorType.Enterprise, MentorType.Academic]);
            (await db.MentorAssignments.AnyAsync(row => row.TeamId == fixture.SiblingTeamId)).Should().BeFalse();
            (await db.OutboxMessages.AnyAsync(row => row.AggregateId == fixture.ClassId)).Should().BeTrue();
            (await db.OutboxMessages.AnyAsync(row => row.AggregateId == fixture.SiblingClassId)).Should().BeFalse();
            var audit = await db.ClassAuditLogs.AsNoTracking().SingleAsync(row => row.ClassId == fixture.ClassId && row.Action == "MENTORS_BALANCED_ASSIGNED");
            using var details = JsonDocument.Parse(audit.DetailsJson!);
            details.RootElement.GetProperty("AiAcceptedBy").GetGuid().Should().Be(admin.Id);
            details.RootElement.GetProperty("AiAcceptedAtUtc").GetDateTime().Kind.Should().Be(DateTimeKind.Utc);
        }

        using var replay = Authorized(HttpMethod.Post, CommitUrl, admin.Token,
            new CommitMentorAllocationRequest { SessionId = preview.SessionId });
        using var repeated = await client.SendAsync(replay);
        repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var profileRequest = Authorized(HttpMethod.Get, "/api/mentoring/profile", fixture.MentorToken);
        using var profileResponse = await client.SendAsync(profileRequest);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await profileResponse.Content.ReadAsStringAsync()).Should().NotContain(PrivateMarker);
        var profile = (await profileResponse.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        profile.Expertise.Should().BeEmpty("this mentor's matching skills come from recorded sessions");
        profile.Experience.Should().BeNull("matching must never save private session notes into the public profile");
        profile.History!.CompletedSessionCount.Should().Be(1);
        profile.History.MentoredTeamCount.Should().Be(1);
        profile.History.AchievementCount.Should().Be(1);
    }

    [Fact]
    public async Task NoEvidenceStillProposesBothTypedSlotsForReviewAndOnlyAcceptanceWritesAssignmentsAndOutbox()
    {
        using var app = App();
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var project = await db.Projects.SingleAsync(row => row.Id == fixture.TargetProjectId);
            project.Name = "Cuneiform tablet conservation";
            project.Description = "Paleography and preservation of ancient clay tablets.";
            await db.SaveChangesAsync();
        }

        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        {
            SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai, Seed = 23
        });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(PrivateMarker);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        preview.CanCommit.Should().BeTrue();
        preview.Unfilled.Should().BeEmpty();
        preview.Assignments.Should().HaveCount(2).And.OnlyContain(row => row.TeamId == fixture.TeamId && row.FitScore == 0);
        preview.Assignments.Select(row => row.MentorType).Should().BeEquivalentTo([nameof(MentorType.Enterprise), nameof(MentorType.Academic)]);
        preview.Assignments.Should().OnlyContain(row => row.Reasons.Any(reason => reason.Contains("cần admin rà soát")));
        preview.Warnings.Should().Contain(message => message.Contains("chưa có bằng chứng chuyên môn phù hợp"));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MentorAssignments.AnyAsync(row => row.TeamId == fixture.TeamId)).Should().BeFalse();
            (await db.OutboxMessages.AnyAsync(row => row.AggregateId == fixture.ClassId)).Should().BeFalse();
        }

        using var commit = Authorized(HttpMethod.Post, CommitUrl, admin.Token,
            new CommitMentorAllocationRequest { SessionId = preview.SessionId });
        using var accepted = await client.SendAsync(commit);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await accepted.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationCommitResponse>>())!.Data!.CreatedCount.Should().Be(2);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.MentorAssignments.AsNoTracking().Where(row => row.TeamId == fixture.TeamId)
                .Include(row => row.MentorProfile).ToArrayAsync();
            saved.Should().HaveCount(2).And.OnlyContain(row => row.Slot == row.MentorProfile.Type &&
                row.Status == MentorAssignmentStatus.Active && row.ProjectId == fixture.TargetProjectId);
            saved.Select(row => row.Slot).Should().BeEquivalentTo([MentorType.Enterprise, MentorType.Academic]);
            (await db.OutboxMessages.AnyAsync(row => row.AggregateId == fixture.ClassId)).Should().BeTrue();
            (await db.MentorAssignments.AnyAsync(row => row.TeamId == fixture.SiblingTeamId)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task EnterpriseTechnicalExperienceProvidesPositiveEvidenceWithoutChangingTypeOrLeakingPrivateHistory()
    {
        using var app = App();
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var project = await db.Projects.SingleAsync(row => row.Id == fixture.TargetProjectId);
            project.Name = "AI Search and LLM Evaluation";
            project.Description = "Build an AI search assistant with retrieval augmented generation and LLM Evaluation.";
            var enterprise = await db.MentorProfiles.SingleAsync(row => row.Id == fixture.EnterpriseProfileId);
            enterprise.Experiences.Add(new MentorExperience
            {
                MentorProfileId = enterprise.Id, Kind = "Technology", Area = "AI Search", Years = 3,
                Notes = "Developed AI Search and LLM Evaluation for retrieval augmented generation assistants."
            });
            await db.SaveChangesAsync();
        }

        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        {
            SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai, Seed = 23
        });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(PrivateMarker);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        var proposed = preview.Assignments.Single(row => row.MentorType == nameof(MentorType.Enterprise));
        proposed.MentorProfileId.Should().Be(fixture.EnterpriseProfileId);
        proposed.FitScore.Should().BeGreaterThan(0);
        proposed.Reasons.Should().Contain(reason => reason.StartsWith("Kinh nghiệm có nội dung liên quan"));
        proposed.Reasons.Should().NotContain(reason => reason.Contains("cần admin rà soát"));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MentorProfiles.AsNoTracking().SingleAsync(row => row.Id == fixture.EnterpriseProfileId))
                .Type.Should().Be(MentorType.Enterprise);
            (await db.MentorAssignments.AnyAsync(row => row.TeamId == fixture.TeamId)).Should().BeFalse();
        }
        using var profileRequest = Authorized(HttpMethod.Get, "/api/mentoring/profile", fixture.MentorToken);
        using var profileResponse = await client.SendAsync(profileRequest);
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await profileResponse.Content.ReadAsStringAsync()).Should().NotContain(PrivateMarker);
    }

    [Fact]
    public async Task EmptyClassScopeProposesAcrossAllClassesWithoutSavingAssignments()
    {
        using var app = App();
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);

        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        {
            SemesterId = fixture.SemesterId, Strategy = MentorAllocationStrategies.Ai, Seed = 23
        });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        preview.TeamCount.Should().Be(2);
        preview.Assignments.Should().HaveCount(4);
        preview.Assignments.Select(row => row.ClassId).Distinct().Should().BeEquivalentTo([fixture.ClassId, fixture.SiblingClassId]);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.MentorAssignments.AnyAsync(row => row.TeamId == fixture.TeamId || row.TeamId == fixture.SiblingTeamId)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(SystemRoles.Student, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Lecturer, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Mentor, HttpStatusCode.Forbidden)]
    public async Task PreviewAndAcceptanceAreAdminOnly(string? role, HttpStatusCode expected)
    {
        using var app = App();
        using var client = app.CreateClient();
        var token = role is null ? null : (await Identity(role)).Token;
        using var preview = Authorized(HttpMethod.Post, PreviewUrl, token, new PreviewMentorAllocationRequest
        {
            SemesterId = Guid.NewGuid(), Strategy = MentorAllocationStrategies.Ai
        });
        using var previewResponse = await client.SendAsync(preview);
        previewResponse.StatusCode.Should().Be(expected);
        using var commit = Authorized(HttpMethod.Post, CommitUrl, token,
            new CommitMentorAllocationRequest { SessionId = Guid.NewGuid() });
        using var commitResponse = await client.SendAsync(commit);
        commitResponse.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ClassFromAnotherSemesterIsRejectedBeforeMatching()
    {
        using var app = App();
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        {
            SemesterId = fixture.SemesterId, ClassIds = [fixture.HistoryClassId], Strategy = MentorAllocationStrategies.Ai
        });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnavailableModelReturns503WithoutCreatingAPartialAllocation()
    {
        using var app = App($"missing-ai-allocation-model-{Guid.NewGuid():N}.json");
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        {
            SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai
        });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.MentorAllocationSessions.AnyAsync(row => row.SemesterId == fixture.SemesterId)).Should().BeFalse();
        (await db.MentorAssignments.AnyAsync(row => row.TeamId == fixture.TeamId)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AcceptanceRejectsTeamsMovedOutsideTheReviewedClassOrAnArchivedClass(bool moveTeam)
    {
        using var app = App();
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        {
            SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai
        });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (moveTeam)
                (await db.Teams.SingleAsync(team => team.Id == fixture.TeamId)).ClassId = fixture.SiblingClassId;
            else
                (await db.Classes.SingleAsync(item => item.Id == fixture.ClassId)).Status = ClassStatus.Archived;
            await db.SaveChangesAsync();
        }

        using var commit = Authorized(HttpMethod.Post, CommitUrl, admin.Token,
            new CommitMentorAllocationRequest { SessionId = preview.SessionId });
        using var accepted = await client.SendAsync(commit);
        accepted.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var verification = factory.Services.CreateScope();
        (await verification.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments
            .AnyAsync(assignment => assignment.TeamId == fixture.TeamId)).Should().BeFalse();
    }

    private WebApplicationFactory<Program> App(string? modelPath = null) => factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.RemoveAll<IMentorSemanticReranker>();
        services.RemoveAll<IMentorFitRanker>();
        services.AddSingleton<IMentorFitRanker>(new FileMentorFitRanker(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mentoring:TrainedModelPath"] = modelPath }).Build()));
    }));

    private async Task<(Guid Id, string Token)> Identity(string roleName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(row => row.Name == roleName);
        var email = $"ai-allocation-{Guid.NewGuid():N}@example.test";
        var user = new User { FullName = "Synthetic AI allocation user", Email = email, NormalizedEmail = email.ToUpperInvariant() };
        user.UserRoles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [roleName]).Token);
    }

    private async Task<Fixture> Seed(Guid adminId)
    {
        var mentor = await Identity(SystemRoles.Mentor);
        var academic = await Identity(SystemRoles.Mentor);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var year = (await db.Semesters.MaxAsync(row => (int?)row.Year) ?? 2026) + 1;
        var previous = new Semester { Code = $"AIP{suffix}", Name = "Synthetic historical semester", Year = year, Term = SemesterTerm.Spring, Status = SemesterStatus.Planned };
        var semester = new Semester { Code = $"AIT{suffix}", Name = "Synthetic allocation semester", Year = year + 1, Term = SemesterTerm.Spring, Status = SemesterStatus.Planned };
        var course = new Course { Code = $"AI{suffix}", Name = "Synthetic startup course", Status = CourseStatus.Active };
        Class NewClass(Semester owner, int index) => new()
        {
            Semester = owner, SemesterId = owner.Id, Course = course, CourseId = course.Id,
            ClassCode = $"AI{suffix}-{owner.Code}-{index}", Slug = $"ai-{suffix}-{owner.Code.ToLowerInvariant()}-{index}", ClassIndex = index,
            PrimaryLecturerId = adminId, Status = ClassStatus.Active, ScheduleJson = "[]", CreatedById = adminId
        };
        var historyClass = NewClass(previous, 1);
        var targetClass = NewClass(semester, 1);
        var siblingClass = NewClass(semester, 2);
        Team NewTeam(Class owner, string code) => new()
        {
            Class = owner, ClassId = owner.Id, TeamCode = code, TeamName = $"Synthetic startup team {code}",
            Description = "Marketing strategy for a startup with a React frontend application.", CreatedById = adminId
        };
        var oldTeam = NewTeam(historyClass, $"AI{suffix}-OLD");
        var target = NewTeam(targetClass, $"AI{suffix}-TARGET");
        target.Project = new Project { Team = target, TeamId = target.Id, Name = "Synthetic current project", CreatedById = adminId };
        var sibling = NewTeam(siblingClass, $"AI{suffix}-SIBLING");
        var enterpriseProfile = new MentorProfile { UserId = mentor.Id, Type = MentorType.Enterprise };
        var academicProfile = new MentorProfile { UserId = academic.Id, Type = MentorType.Academic, Expertise = ["React"] };
        var now = DateTime.UtcNow;
        var project = new Project
        {
            Team = oldTeam, TeamId = oldTeam.Id, Name = "Synthetic historical project", IsAwarded = true,
            AchievementsUpdatedAt = now.AddDays(-1), AchievementNote = "Note edited after the original mentor ended", CreatedById = adminId
        };
        db.ProjectActivityLogs.AddRange(
            new ProjectActivityLog
            {
                Project = project, ProjectId = project.Id, ActorUserId = adminId, Action = "PROJECT_ACHIEVEMENTS_CHANGED",
                Summary = "Achievements changed from none to Awarded.",
                ChangedFieldsJson = JsonSerializer.Serialize(new[] { "+Awarded", "note=Synthetic project award" }),
                OccurredAtUtc = now.AddDays(-4)
            },
            new ProjectActivityLog
            {
                Project = project, ProjectId = project.Id, ActorUserId = adminId, Action = "PROJECT_ACHIEVEMENTS_CHANGED",
                Summary = "Achievement note updated.",
                ChangedFieldsJson = JsonSerializer.Serialize(new[] { "=Awarded", "note=Edited later" }),
                OccurredAtUtc = now.AddDays(-1)
            });
        var assignment = new MentorAssignment
        {
            MentorProfile = enterpriseProfile, MentorProfileId = enterpriseProfile.Id, Team = oldTeam, TeamId = oldTeam.Id,
            Project = project, ProjectId = project.Id, AssignedById = adminId, AssignedAt = now.AddDays(-30),
            EndedAt = now.AddDays(-2), Status = MentorAssignmentStatus.Ended, Slot = MentorType.Enterprise
        };
        db.MentoringSessions.Add(new MentoringSession
        {
            MentorAssignment = assignment, MentorAssignmentId = assignment.Id, Title = "Coaching review",
            Notes = $"Marketing strategy and customer acquisition. {PrivateMarker}",
            StartAt = new DateTimeOffset(now.AddDays(-5)), EndAt = new DateTimeOffset(now.AddDays(-5).AddHours(1)),
            Status = MentoringSessionStatus.Completed
        });
        db.MentorProfiles.Add(academicProfile);
        db.Teams.AddRange(target, sibling);
        foreach (var userId in new[] { mentor.Id, academic.Id })
            db.SemesterStaffAssignments.Add(new SemesterStaffAssignment
            {
                Semester = semester, SemesterId = semester.Id, UserId = userId, Role = SemesterStaffRole.Mentor,
                Status = SemesterStaffStatus.Active, CreatedBy = adminId
            });
        await db.SaveChangesAsync();
        return new Fixture(semester.Id, targetClass.Id, target.Id, siblingClass.Id, sibling.Id, historyClass.Id,
            enterpriseProfile.Id, target.Project.Id, mentor.Token);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string? token, object? payload = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return request;
    }

    private sealed record Fixture(Guid SemesterId, Guid ClassId, Guid TeamId, Guid SiblingClassId,
        Guid SiblingTeamId, Guid HistoryClassId, Guid EnterpriseProfileId, Guid TargetProjectId, string MentorToken);
}
