using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.Scoring;
using EHub.Contracts.Common;
using EHub.Contracts.Mentors;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Mentoring.Gemini;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
        enterprise.History.HistoryBonus.Should().Be(3);
        enterprise.History.AchievementBonus.Should().Be(3);
        enterprise.FitScore.Should().Be(Math.Min(100, enterprise.History.BaseFitScore + 6));
        enterprise.Reasons.Should().NotContain(reason => reason.Contains("buổi"));

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
        profile.Expertise.Should().BeEmpty("professional experience remains separate from expertise tags");
        profile.Experience.Should().Be(RecordingGemini.Evidence, "matching must never save private session notes into the public profile");
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
    public async Task EnterprisePublicTechnicalExperienceProvidesPositiveEvidenceWithoutChangingTypeOrLeakingPrivateHistory()
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
            enterprise.Experience = "Developed AI Search and LLM Evaluation for retrieval augmented generation assistants.";
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
        proposed.Reasons.Should().Contain(reason => reason.Contains("bằng chứng: AI"));
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
        using var app = App(unavailable: true);
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

    [Fact]
    public async Task GeminiHandles100ProjectsAnd30MentorsInSeparateRoleCallsAndOnlyAdminAcceptanceSaves()
    {
        var gemini = new RecordingGemini();
        using var app = App(gemini: gemini);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var role = await db.Roles.SingleAsync(item => item.Name == SystemRoles.Mentor);
            var profiles = await db.MentorProfiles.Where(profile => db.SemesterStaffAssignments.Any(staff =>
                staff.SemesterId == fixture.SemesterId && staff.UserId == profile.UserId)).ToListAsync();
            foreach (var profile in profiles) profile.Experience = RecordingGemini.Evidence;
            for (var index = 0; index < 28; index++)
            {
                var suffix = Guid.NewGuid().ToString("N");
                var user = new User { FullName = "Synthetic Gemini mentor", Email = $"gemini-{suffix}@example.test",
                    NormalizedEmail = $"GEMINI-{suffix}@EXAMPLE.TEST" };
                user.UserRoles.Add(new UserRole { User = user, Role = role });
                db.MentorProfiles.Add(new MentorProfile { User = user, Experience = RecordingGemini.Evidence,
                    Type = index % 2 == 0 ? MentorType.Enterprise : MentorType.Academic });
                db.SemesterStaffAssignments.Add(new SemesterStaffAssignment { SemesterId = fixture.SemesterId,
                    User = user, Role = SemesterStaffRole.Mentor, Status = SemesterStaffStatus.Active, CreatedBy = admin.Id });
            }
            for (var index = 1; index < 100; index++)
            {
                var team = new Team { ClassId = fixture.ClassId, TeamCode = $"GEM-{Guid.NewGuid():N}",
                    TeamName = $"Fictional Gemini project {index}", Description = $"Marketing startup with React number {index}", CreatedById = admin.Id };
                team.Project = new Project { Team = team, TeamId = team.Id, Name = team.TeamName,
                    Description = team.Description, CreatedById = admin.Id };
                db.Teams.Add(team);
            }
            await db.SaveChangesAsync();
        }
        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        {
            SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai, Seed = 23
        });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        preview.TeamCount.Should().Be(100);
        preview.Assignments.Should().HaveCount(200);
        preview.Assignments.Should().OnlyContain(item => item.FitScore >= 0);
        preview.Assignments.Should().Contain(item => item.FitScore > 0 && item.Reasons.Any(reason => reason.Contains("Gemini")));
        preview.Assignments.Where(item => item.FitScore == 0).Should().OnlyContain(item => item.Reasons.Any(reason => reason.Contains("cần admin rà soát")));
        foreach (var type in new[] { nameof(MentorType.Enterprise), nameof(MentorType.Academic) })
        {
            var loads = preview.MentorLoads.Where(item => item.MentorType == type).Select(item => item.TotalAfter).ToArray();
            loads.Should().HaveCount(15);
            loads.Min().Should().Be(6);
            loads.Max().Should().Be(7);
            var balance = preview.Balance.Single(item => item.MentorType == type);
            balance.SemesterTeamCount.Should().Be(101, "the other class is included in semester totals");
            balance.EligibleMentorCount.Should().Be(15);
            balance.TargetMinTeams.Should().Be(6);
            balance.TargetMaxTeams.Should().Be(7);
        }
        preview.CanCommit.Should().BeTrue();
        gemini.Inputs.Should().HaveCount(2);
        gemini.ProjectCount.Should().Be(100);
        gemini.Batches.Should().BeEquivalentTo([("Business", 100, 15), ("IT", 100, 15)]);
        gemini.Inputs.Should().OnlyContain(input => !input.Contains(PrivateMarker) && !input.Contains(admin.Id.ToString()));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MentorAssignments.CountAsync(item => item.Team.ClassId == fixture.ClassId)).Should().Be(0);
            (await db.OutboxMessages.CountAsync(item => item.AggregateId == fixture.ClassId)).Should().Be(0);
        }
        using var commit = Authorized(HttpMethod.Post, CommitUrl, admin.Token, new CommitMentorAllocationRequest { SessionId = preview.SessionId });
        using var accepted = await client.SendAsync(commit);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MentorAssignments.CountAsync(item => item.Team.ClassId == fixture.ClassId)).Should().Be(200);
            (await db.OutboxMessages.AnyAsync(item => item.AggregateId == fixture.ClassId)).Should().BeTrue();
            (await db.MentorAssignments.AnyAsync(item => item.TeamId == fixture.SiblingTeamId)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task BackendRanksFiveCriteriaAndIgnoresAnExtraOverallModelScore()
    {
        var gemini = new RecordingGemini { CompareBackendRules = true };
        using var app = App(gemini: gemini);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var project = await db.Projects.SingleAsync(row => row.Id == fixture.TargetProjectId);
            project.Name = "Marketing";
            project.Description = "Marketing";
            var profiles = await db.MentorProfiles.Where(profile => db.SemesterStaffAssignments.Any(staff =>
                staff.SemesterId == fixture.SemesterId && staff.UserId == profile.UserId)).ToArrayAsync();
            foreach (var profile in profiles) profile.Experience = RecordingGemini.Evidence;
            var strong = profiles.Single(profile => profile.Id == fixture.EnterpriseProfileId);
            strong.Expertise = ["Marketing"];
            strong.StartupDomains = ["Marketing"];
            strong.TechnologySkills = ["Marketing"];
            strong.Bio = "Marketing";
            var role = await db.Roles.SingleAsync(row => row.Name == SystemRoles.Mentor);
            var email = $"low-rule-{Guid.NewGuid():N}@example.test";
            var user = new User { FullName = "Synthetic lower-coverage mentor", Email = email, NormalizedEmail = email.ToUpperInvariant() };
            user.UserRoles.Add(new UserRole { User = user, Role = role });
            db.MentorProfiles.Add(new MentorProfile
            {
                User = user, Type = MentorType.Enterprise, Experience = RecordingGemini.Evidence + " LOW_RULE_PROFILE"
            });
            db.SemesterStaffAssignments.Add(new SemesterStaffAssignment
            {
                SemesterId = fixture.SemesterId, User = user, Role = SemesterStaffRole.Mentor,
                Status = SemesterStaffStatus.Active, CreatedBy = admin.Id
            });
            await db.SaveChangesAsync();
        }

        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        { SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain(PrivateMarker);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        var enterprise = preview.Assignments.Single(row => row.MentorType == nameof(MentorType.Enterprise));
        enterprise.MentorProfileId.Should().Be(fixture.EnterpriseProfileId,
            "five criterion ratings, not the ignored legacy overall model score, determine the ranking");
        enterprise.History!.BaseFitScore.Should().Be(70);
        enterprise.FitScore.Should().Be(76); // 70% * profile 100 + three history points per team + achievement 3.
        enterprise.Reasons.Should().Contain(reason => reason.Contains("70% × Hồ sơ 100 = 70"));
        enterprise.Reasons.Should().NotContain(reason => reason.Contains("80%"));
        gemini.Inputs.Should().HaveCount(2).And.OnlyContain(input => !input.Contains(PrivateMarker));
        using var verification = factory.Services.CreateScope();
        (await verification.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments
            .AnyAsync(row => row.TeamId == fixture.TeamId)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(SystemRoles.Student, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Mentor, HttpStatusCode.Forbidden)]
    [InlineData(SystemRoles.Lecturer, HttpStatusCode.Forbidden)]
    public async Task GeminiDoesNotReceiveAnyDataBeforeAuthenticationAndAdminAuthorization(string? role, HttpStatusCode expected)
    {
        var gemini = new RecordingGemini();
        using var app = App(gemini: gemini);
        using var client = app.CreateClient();
        var token = role is null ? null : (await Identity(role)).Token;
        using var request = Authorized(HttpMethod.Post, PreviewUrl, token, new PreviewMentorAllocationRequest
        { SemesterId = Guid.NewGuid(), Strategy = MentorAllocationStrategies.Ai });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(expected);
        gemini.Inputs.Should().BeEmpty();
    }

    [Fact]
    public async Task AdminCanAcceptManualEditsAboveTheAutomaticBalanceTarget()
    {
        using var app = App();
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        var extraMentor = await Identity(SystemRoles.Mentor);
        var teamIds = new List<Guid> { fixture.TeamId };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MentorProfiles.Add(new() { UserId = extraMentor.Id, Type = MentorType.Enterprise, Expertise = ["Marketing"] });
            db.SemesterStaffAssignments.Add(new()
            {
                SemesterId = fixture.SemesterId, UserId = extraMentor.Id, Role = SemesterStaffRole.Mentor,
                Status = SemesterStaffStatus.Active, CreatedBy = admin.Id
            });
            for (var index = 0; index < 2; index++)
            {
                var team = new Team
                {
                    ClassId = fixture.ClassId, TeamCode = $"MANUAL-{Guid.NewGuid():N}", TeamName = $"Synthetic manual team {index}",
                    Description = "React marketing application", CreatedById = admin.Id
                };
                team.Project = new Project { Team = team, Name = "Synthetic manual project", CreatedById = admin.Id };
                db.Teams.Add(team);
                teamIds.Add(team.Id);
            }
            await db.SaveChangesAsync();
        }
        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        {
            SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai,
            Edits = teamIds.Select(teamId => new MentorAllocationEdit
            {
                TeamId = teamId, MentorType = nameof(MentorType.Enterprise), MentorProfileId = fixture.EnterpriseProfileId
            }).ToArray()
        });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        preview.Assignments.Where(row => row.MentorType == nameof(MentorType.Enterprise)).Should()
            .HaveCount(3).And.OnlyContain(row => row.Source == MentorAllocationSources.Manual && row.MentorProfileId == fixture.EnterpriseProfileId);
        var balance = preview.Balance.Single(row => row.MentorType == nameof(MentorType.Enterprise));
        balance.SemesterTeamCount.Should().Be(4);
        balance.EligibleMentorCount.Should().Be(2);
        balance.TargetMaxTeams.Should().Be(2);
        balance.MinLoadAfter.Should().Be(0);
        balance.MaxLoadAfter.Should().Be(3);
        preview.Warnings.Should().Contain(message => message.Contains("chênh lệch"));
        preview.CanCommit.Should().BeTrue();
        using (var before = factory.Services.CreateScope())
            (await before.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.CountAsync(row => teamIds.Contains(row.TeamId)))
                .Should().Be(0);

        using var commit = Authorized(HttpMethod.Post, CommitUrl, admin.Token, new CommitMentorAllocationRequest { SessionId = preview.SessionId });
        using var accepted = await client.SendAsync(commit);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verified = factory.Services.CreateScope();
        (await verified.ServiceProvider.GetRequiredService<AppDbContext>().MentorAssignments.CountAsync(row =>
            teamIds.Contains(row.TeamId) && row.Slot == MentorType.Enterprise && row.MentorProfileId == fixture.EnterpriseProfileId))
            .Should().Be(3);
    }

    [Fact]
    public async Task InvalidGeminiOutputReturns503WithoutCreatingAssignmentsOrAPreviewSession()
    {
        var gemini = new RecordingGemini { InvalidOutput = true };
        using var app = App(gemini: gemini);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        { SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(PrivateMarker).And.NotContain("OFFLINE_GEMINI_TEST_KEY");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.MentorAssignments.AnyAsync(item => item.TeamId == fixture.TeamId)).Should().BeFalse();
        (await db.MentorAllocationSessions.AnyAsync(item => item.SemesterId == fixture.SemesterId)).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GeminiOnlyScoresEmptySlotsAndNeverRescoresOrReplacesExistingMentors(int occupiedCount)
    {
        var gemini = new RecordingGemini();
        using var app = App(gemini: gemini);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        var existingIds = new List<Guid>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profiles = await db.MentorProfiles.Where(profile => db.SemesterStaffAssignments.Any(staff =>
                staff.SemesterId == fixture.SemesterId && staff.UserId == profile.UserId)).OrderBy(profile => profile.Type).ToArrayAsync();
            foreach (var profile in profiles) profile.Experience = RecordingGemini.Evidence;
            foreach (var profile in profiles.Take(occupiedCount))
            {
                var existing = new MentorAssignment { TeamId = fixture.TeamId, ProjectId = fixture.TargetProjectId,
                    MentorProfileId = profile.Id, Slot = profile.Type, Status = MentorAssignmentStatus.Active,
                    AssignedById = admin.Id, AssignedAt = DateTime.UtcNow.AddDays(-1) };
                db.MentorAssignments.Add(existing);
                existingIds.Add(existing.Id);
            }
            await db.SaveChangesAsync();
        }
        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        { SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        preview.Assignments.Should().HaveCount(2 - occupiedCount).And.OnlyContain(row => row.FitScore > 0);
        preview.ExistingAssignments.Should().HaveCount(occupiedCount).And.OnlyContain(row => !row.Replaced);
        gemini.Inputs.Should().HaveCount(2 - occupiedCount);
        if (occupiedCount == 1)
        {
            var existingType = preview.ExistingAssignments.Single().MentorType == nameof(MentorType.Enterprise) ? "Business" : "IT";
            gemini.Batches.Should().OnlyContain(batch => batch.Role != existingType);
        }
        if (occupiedCount == 2)
        {
            preview.CanCommit.Should().BeFalse();
            preview.SessionId.Should().BeEmpty();
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.MentorAssignments.CountAsync(row => row.TeamId == fixture.TeamId)).Should().Be(occupiedCount);
            (await db.MentorAssignments.Where(row => existingIds.Contains(row.Id)).ToArrayAsync())
                .Should().OnlyContain(row => row.EndedAt == null && row.Status == MentorAssignmentStatus.Active);
        }
        if (occupiedCount == 2) return;
        using var commit = Authorized(HttpMethod.Post, CommitUrl, admin.Token, new CommitMentorAllocationRequest { SessionId = preview.SessionId });
        using var accepted = await client.SendAsync(commit);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await accepted.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationCommitResponse>>())!.Data!.CreatedCount.Should().Be(2 - occupiedCount);
        using var verification = factory.Services.CreateScope();
        var verified = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verified.MentorAssignments.CountAsync(row => row.TeamId == fixture.TeamId)).Should().Be(2);
        (await verified.MentorAssignments.Where(row => existingIds.Contains(row.Id)).ToArrayAsync())
            .Should().HaveCount(occupiedCount).And.OnlyContain(row => row.EndedAt == null && row.Status == MentorAssignmentStatus.Active);
    }

    [Fact]
    public async Task IdenticalProjectTextWithDifferentMissingSlotsStillScoresBothRolesIndependently()
    {
        var gemini = new RecordingGemini();
        using var app = App(gemini: gemini);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var target = await db.Teams.Include(team => team.Project).SingleAsync(team => team.Id == fixture.TeamId);
            var sibling = await db.Teams.SingleAsync(team => team.Id == fixture.SiblingTeamId);
            target.Project!.Name = "Shared marketing and React project";
            target.Project.Description = "Marketing strategy and React frontend development";
            sibling.Project = new Project { Team = sibling, Name = target.Project.Name, Description = target.Project.Description, CreatedById = admin.Id };
            db.Projects.Add(sibling.Project);
            var profiles = await db.MentorProfiles.Where(profile => db.SemesterStaffAssignments.Any(staff =>
                staff.SemesterId == fixture.SemesterId && staff.UserId == profile.UserId)).ToArrayAsync();
            foreach (var profile in profiles) profile.Experience = RecordingGemini.Evidence;
            foreach (var profile in profiles)
                db.MentorAssignments.Add(new() { TeamId = profile.Type == MentorType.Enterprise ? target.Id : sibling.Id,
                    MentorProfileId = profile.Id, Slot = profile.Type, Status = MentorAssignmentStatus.Active,
                    AssignedById = admin.Id, AssignedAt = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        }
        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        { SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId, fixture.SiblingClassId], Strategy = MentorAllocationStrategies.Ai });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorAllocationPreviewResponse>>())!.Data!;
        preview.Assignments.Should().HaveCount(2).And.OnlyContain(row => row.FitScore > 0);
        preview.Assignments.Should().ContainSingle(row => row.TeamId == fixture.TeamId && row.MentorType == nameof(MentorType.Academic));
        preview.Assignments.Should().ContainSingle(row => row.TeamId == fixture.SiblingTeamId && row.MentorType == nameof(MentorType.Enterprise));
        preview.ExistingAssignments.Should().HaveCount(2).And.OnlyContain(row => !row.Replaced);
        gemini.Batches.Should().BeEquivalentTo([("Business", 1, 1), ("IT", 1, 1)]);
    }

    [Theory]
    [InlineData("Business", 1)]
    [InlineData("IT", 2)]
    public async Task AFailedRoleCannotBecomeAZeroScoreFallbackOrPartiallySavedPreview(string invalidRole, int expectedCalls)
    {
        var gemini = new RecordingGemini { InvalidRole = invalidRole };
        using var app = App(gemini: gemini);
        using var client = app.CreateClient();
        var admin = await Identity(SystemRoles.Admin);
        var fixture = await Seed(admin.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var profile in await db.MentorProfiles.Where(profile => db.SemesterStaffAssignments.Any(staff =>
                         staff.SemesterId == fixture.SemesterId && staff.UserId == profile.UserId)).ToArrayAsync())
                profile.Experience = RecordingGemini.Evidence;
            await db.SaveChangesAsync();
        }
        using var request = Authorized(HttpMethod.Post, PreviewUrl, admin.Token, new PreviewMentorAllocationRequest
        { SemesterId = fixture.SemesterId, ClassIds = [fixture.ClassId], Strategy = MentorAllocationStrategies.Ai });
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        gemini.Inputs.Should().HaveCount(expectedCalls);
        using var verification = factory.Services.CreateScope();
        var dbContext = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.MentorAllocationSessions.AnyAsync(row => row.SemesterId == fixture.SemesterId)).Should().BeFalse();
        (await dbContext.MentorAssignments.AnyAsync(row => row.TeamId == fixture.TeamId)).Should().BeFalse();
    }

    private WebApplicationFactory<Program> App(bool unavailable = false, RecordingGemini? gemini = null) => factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.AddSingleton<IOptions<MentorScoringOptions>>(Options.Create(new MentorScoringOptions()));
        services.AddSingleton<IMentorEvidenceGuard>(new FileMentorEvidenceGuard(new ConfigurationBuilder().Build()));
        services.AddSingleton<IOptions<GeminiMentorOptions>>(Options.Create(new GeminiMentorOptions
            { ApiKey = "SYNTHETIC_GEMINI_TEST_KEY", DataTransferEnabled = true, MinimumRequestIntervalSeconds = 0 }));
        services.AddSingleton<GeminiRequestCoordinator>();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<IGenerativeMentorFitRanker, GeminiMentorFitRanker>()
            .ConfigurePrimaryHttpMessageHandler(() => gemini is not null ? gemini : new SyntheticGeminiHandler
                { FailureStatus = unavailable ? HttpStatusCode.ServiceUnavailable : null });
    }));

    private sealed class RecordingGemini : HttpMessageHandler
    {
        public const string Evidence = "Marketing strategy and React frontend development";
        public List<string> Inputs { get; } = [];
        public bool InvalidOutput { get; init; }
        public bool CompareBackendRules { get; init; }
        public string? InvalidRole { get; init; }
        public List<(string Role, int Projects, int Mentors)> Batches { get; } = [];
        public int ProjectCount { get; private set; }
        public int MentorCount { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Inputs.Add(body);
            using var envelope = JsonDocument.Parse(body);
            using var data = JsonDocument.Parse(envelope.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
            var projects = data.RootElement.GetProperty("projects").EnumerateArray().ToArray();
            var mentors = data.RootElement.GetProperty("mentors").EnumerateArray().ToArray();
            ProjectCount = projects.Length;
            MentorCount = mentors.Length;
            var role = data.RootElement.GetProperty("mentorRole").GetString()!;
            mentors.Select(item => item.GetProperty("role").GetString()).Distinct().Should().Equal(role);
            Batches.Add((role, projects.Length, mentors.Length));
            var selected = mentors.GroupBy(item => item.GetProperty("role").GetString())
                .SelectMany(group => group.Take(CompareBackendRules ? 2 : 1)).ToArray();
            var text = InvalidOutput || InvalidRole == role ? "{\"results\":[]}" : JsonSerializer.Serialize(new
            {
                results = projects.Select(project => new
                {
                    project = project.GetProperty("id").GetString(),
                    mentors = selected.Select(mentor =>
                    {
                        var candidate = new Dictionary<string, object?>
                        {
                            ["id"] = mentor.GetProperty("id").GetString(),
                            ["criteria"] = new[] { "experience", "expertise", "startupDomain", "technology", "bio" }.ToDictionary(name => name, name =>
                            {
                                var field = mentor.GetProperty("profile").GetProperty(name).GetString()!;
                                var quote = name == "experience" ? Evidence : field.Contains("Marketing") ? "Marketing" : "";
                                return new { score = quote.Length > 0 ? 100 : 0, evidence = quote };
                            })
                        };
                        if (CompareBackendRules) candidate["score"] = mentor.GetProperty("profile").GetProperty("experience").GetString()!.Contains("LOW_RULE_PROFILE") ? 99 : 40;
                        return candidate;
                    })
                })
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text } } } } } }) };
        }
    }

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
        var enterpriseProfile = new MentorProfile { UserId = mentor.Id, Type = MentorType.Enterprise, Experience = RecordingGemini.Evidence };
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
