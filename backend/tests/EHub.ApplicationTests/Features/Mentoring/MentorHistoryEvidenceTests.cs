using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Mentoring.History;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorHistoryEvidenceTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HistoryUsesOnlyCompletedSessionsInsideTheActualAssignmentInterval()
    {
        await using var db = Context();
        var mentor = AddMentor(db);
        var assignment = AddAssignment(db, mentor);
        assignment.Status = MentorAssignmentStatus.Ended;
        assignment.EndedAt = Now.AddDays(-1);
        AddSession(db, assignment, "Marketing guidance", Now.AddDays(-3));
        AddSession(db, assignment, "SECOND_VALID_SESSION", Now.AddDays(-2));
        AddSession(db, assignment, "FUTURE_SESSION", Now.AddDays(1));
        AddSession(db, assignment, "BEFORE_ASSIGNMENT", Now.AddDays(-11));
        AddSession(db, assignment, "AFTER_ASSIGNMENT", Now.AddHours(-2));
        AddSession(db, assignment, "SCHEDULED_SESSION", Now.AddDays(-2), MentoringSessionStatus.Scheduled);
        AddSession(db, assignment, "CANCELLED_SESSION", Now.AddDays(-2), MentoringSessionStatus.Cancelled);
        AddSession(db, assignment, "NO_SHOW_SESSION", Now.AddDays(-2), MentoringSessionStatus.NoShow);
        AddSession(db, assignment, "DELETED_SESSION", Now.AddDays(-2)).IsDeleted = true;
        var cancelled = AddAssignment(db, mentor);
        cancelled.Status = MentorAssignmentStatus.Cancelled;
        AddSession(db, cancelled, "CANCELLED_ASSIGNMENT", Now.AddDays(-2));
        var pending = AddAssignment(db, mentor);
        pending.Status = MentorAssignmentStatus.Pending;
        AddSession(db, pending, "PENDING_ASSIGNMENT", Now.AddDays(-2));
        await db.SaveChangesAsync();

        var result = (await MentorHistoryEvidenceLoader.LoadAsync(db, [mentor.Id], Now, default))[mentor.Id];

        result.CompletedSessionCount.Should().Be(2);
        result.MentoredTeamCount.Should().Be(1);
        result.EvidenceText.Should().Contain("Marketing guidance").And.Contain("SECOND_VALID_SESSION")
            .And.NotContain("FUTURE_SESSION").And.NotContain("BEFORE_ASSIGNMENT").And.NotContain("AFTER_ASSIGNMENT")
            .And.NotContain("SCHEDULED_SESSION").And.NotContain("CANCELLED_SESSION").And.NotContain("NO_SHOW_SESSION")
            .And.NotContain("DELETED_SESSION").And.NotContain("CANCELLED_ASSIGNMENT").And.NotContain("PENDING_ASSIGNMENT");
    }

    [Fact]
    public async Task ProjectAchievementsRequireAnExplicitLinkAndTimestampDuringTheAssignment()
    {
        await using var db = Context();
        var mentor = AddMentor(db);
        var valid = AddAssignment(db, mentor);
        valid.Project = new Project { Team = valid.Team, Name = "Historical project", IsAwarded = true,
            AchievementNote = "Market research award", AchievementsUpdatedAt = Now.AddDays(-3) };
        var repeated = AddAssignment(db, mentor);
        repeated.Team = valid.Team;
        repeated.Status = MentorAssignmentStatus.Ended;
        repeated.EndedAt = Now.AddDays(-2);
        repeated.Project = valid.Project;
        var inherited = AddAssignment(db, mentor);
        inherited.Project = new Project { Team = inherited.Team, Name = "Continued project", IsAwarded = true,
            AchievementNote = "INHERITED_AWARD", AchievementsUpdatedAt = Now.AddDays(-12) };
        var ended = AddAssignment(db, mentor);
        ended.Status = MentorAssignmentStatus.Ended;
        ended.EndedAt = Now.AddDays(-4);
        ended.Project = new Project { Team = ended.Team, Name = "Later award", IsAwarded = true,
            AchievementNote = "LATER_AWARD", AchievementsUpdatedAt = Now.AddDays(-3) };
        var unlinked = AddAssignment(db, mentor);
        unlinked.Team.Project = new Project { Name = "Unlinked team project", IsAwarded = true,
            AchievementNote = "UNLINKED_AWARD", AchievementsUpdatedAt = Now.AddDays(-3) };
        var future = AddAssignment(db, mentor);
        future.Project = new Project { Team = future.Team, Name = "Future award", IsAwarded = true,
            AchievementNote = "FUTURE_AWARD", AchievementsUpdatedAt = Now.AddDays(1) };
        db.Projects.AddRange(valid.Project, inherited.Project, ended.Project, unlinked.Team.Project, future.Project);
        await db.SaveChangesAsync();

        var result = (await MentorHistoryEvidenceLoader.LoadAsync(db, [mentor.Id], Now, default))[mentor.Id];

        result.AchievementCount.Should().Be(1);
        result.CompletedSessionCount.Should().Be(0);
        result.EvidenceText.Should().Contain("Market research award").And.NotContain("INHERITED_AWARD")
            .And.NotContain("LATER_AWARD").And.NotContain("UNLINKED_AWARD").And.NotContain("FUTURE_AWARD");
    }

    [Fact]
    public async Task RecentEvidenceIsBoundedButCountsCoverAllEligibleSessions()
    {
        await using var db = Context();
        var mentor = AddMentor(db);
        var other = AddMentor(db);
        var assignment = AddAssignment(db, mentor);
        assignment.AssignedAt = Now.AddDays(-100);
        for (var index = 0; index < 40; index++)
        {
            var session = AddSession(db, assignment, "Coaching session", Now.AddDays(-index - 1));
            session.Notes = index == 39 ? "OLDEST_SESSION_NOTES" : new string('a', 10000);
        }
        AddSession(db, AddAssignment(db, other), "UNREQUESTED_MENTOR_NOTES", Now.AddDays(-3));
        await db.SaveChangesAsync();

        var result = await MentorHistoryEvidenceLoader.LoadAsync(db, [mentor.Id, mentor.Id], Now, default);

        result.Should().ContainSingle();
        result[mentor.Id].CompletedSessionCount.Should().Be(40);
        result[mentor.Id].MentoredTeamCount.Should().Be(1);
        result[mentor.Id].EvidenceText.Length.Should().BeLessThanOrEqualTo(3000);
        result[mentor.Id].EvidenceText.Should().NotContain("OLDEST_SESSION_NOTES").And.NotContain("UNREQUESTED_MENTOR_NOTES");
    }

    [Fact]
    public async Task LaterNoteEditsKeepTheOriginalMentorsAchievementWithoutCreditingTheReplacement()
    {
        await using var db = Context();
        var original = AddMentor(db);
        var replacement = AddMentor(db);
        var originalAssignment = AddAssignment(db, original);
        originalAssignment.Status = MentorAssignmentStatus.Ended;
        originalAssignment.EndedAt = Now.AddDays(-5);
        var project = new Project
        {
            Team = originalAssignment.Team, Name = "Historical award", IsAwarded = true,
            AchievementNote = "PRIVATE_LATER_NOTE", AchievementsUpdatedAt = Now.AddDays(-1)
        };
        originalAssignment.Project = project;
        var replacementAssignment = AddAssignment(db, replacement);
        replacementAssignment.Team = originalAssignment.Team;
        replacementAssignment.Project = project;
        replacementAssignment.AssignedAt = Now.AddDays(-4);
        db.Projects.Add(project);
        AddAchievementLog(db, project, Now.AddDays(-7), "+Awarded", "note=Original marketing research award");
        AddAchievementLog(db, project, Now.AddDays(-1), "=Awarded", "note=PRIVATE_LATER_NOTE");
        await db.SaveChangesAsync();

        var result = await MentorHistoryEvidenceLoader.LoadAsync(db, [original.Id, replacement.Id], Now, default);

        result[original.Id].AchievementCount.Should().Be(1);
        result[original.Id].EvidenceText.Should().Contain("Original marketing research award").And.NotContain("PRIVATE_LATER_NOTE");
        result[replacement.Id].AchievementCount.Should().Be(0);
        result[replacement.Id].EvidenceText.Should().NotContain("PRIVATE_LATER_NOTE");
    }

    [Fact]
    public async Task CarriedOverRemovedNoteOnlyAndOutOfIntervalEventsCannotCreditCurrentSnapshot()
    {
        await using var db = Context();
        var mentor = AddMentor(db);
        var cases = new[]
        {
            (Action: "ACHIEVEMENTS_CARRIED_OVER", At: Now.AddDays(-2), Token: "+Awarded", Marker: "CARRIED_OVER"),
            (Action: "PROJECT_ACHIEVEMENTS_CHANGED", At: Now.AddDays(-2), Token: "-Awarded", Marker: "REMOVED"),
            (Action: "PROJECT_ACHIEVEMENTS_CHANGED", At: Now.AddDays(-2), Token: "=Awarded", Marker: "NOTE_ONLY"),
            (Action: "PROJECT_ACHIEVEMENTS_CHANGED", At: Now.AddDays(1), Token: "+Awarded", Marker: "FUTURE"),
            (Action: "PROJECT_ACHIEVEMENTS_CHANGED", At: Now.AddDays(-12), Token: "+Awarded", Marker: "BEFORE_ASSIGNMENT")
        };
        foreach (var item in cases)
        {
            var assignment = AddAssignment(db, mentor);
            var project = new Project { Team = assignment.Team, Name = item.Marker, IsAwarded = true,
                AchievementNote = item.Marker, AchievementsUpdatedAt = Now.AddDays(-2) };
            assignment.Project = project;
            db.Projects.Add(project);
            var activity = AddAchievementLog(db, project, item.At, item.Token, "note=" + item.Marker);
            activity.Action = item.Action;
        }
        await db.SaveChangesAsync();

        var result = (await MentorHistoryEvidenceLoader.LoadAsync(db, [mentor.Id], Now, default))[mentor.Id];

        result.AchievementCount.Should().Be(0);
        foreach (var item in cases) result.EvidenceText.Should().NotContain(item.Marker);
    }

    [Fact]
    public async Task LegacyAchievementSummariesUseTheirEventDateAndDeduplicateRepeatedAssignments()
    {
        await using var db = Context();
        var mentor = AddMentor(db);
        var assignment = AddAssignment(db, mentor);
        var project = new Project { Team = assignment.Team, Name = "Legacy achievement" };
        assignment.Project = project;
        var repeated = AddAssignment(db, mentor);
        repeated.Team = assignment.Team;
        repeated.Project = project;
        db.Projects.Add(project);
        var activity = AddAchievementLog(db, project, Now.AddDays(-3));
        activity.ChangedFieldsJson = "[\"note\"]";
        activity.Summary = "Achievements changed from none to Awarded. Note: \"Legacy market research award\"";
        await db.SaveChangesAsync();

        var result = (await MentorHistoryEvidenceLoader.LoadAsync(db, [mentor.Id], Now, default))[mentor.Id];

        result.AchievementCount.Should().Be(1);
        result.EvidenceText.Should().Contain("Legacy market research award");
    }

    [Fact]
    public async Task CoachingNotesSupplyMatchingEvidenceWithoutChangingTheSavedProfile()
    {
        await using var db = Context();
        var mentor = AddMentor(db);
        mentor.Type = MentorType.Academic;
        mentor.Experience = "General mentoring background";
        var session = AddSession(db, AddAssignment(db, mentor), "Technical coaching", Now.AddDays(-3));
        session.Notes = "Guided C#/.NET backend implementation and PostgreSQL database design.";
        await db.SaveChangesAsync();
        var history = await MentorHistoryEvidenceLoader.LoadAsync(db, [mentor.Id], Now, default);
        var prepared = MentorHistoryEvidenceLoader.ForRanking([mentor], history);
        var ranker = new FileMentorFitRanker(new ConfigurationBuilder().Build());

        var originalRanking = ranker.Rank("Backend C#/.NET và PostgreSQL", [mentor], default)[mentor.Id];
        var historyRanking = ranker.Rank("Backend C#/.NET và PostgreSQL", prepared, default)[mentor.Id];
        var explained = MentorHistoryEvidenceLoader.AddReasons(historyRanking, history[mentor.Id]);

        originalRanking.HasEvidence.Should().BeFalse();
        historyRanking.HasEvidence.Should().BeTrue();
        explained.Reasons.Should().Contain(reason => reason.Contains("1 buổi hướng dẫn"));
        explained.Reasons.Should().NotContain(reason => reason.Contains("Guided C#/.NET backend implementation"));
        explained.Score.Should().Be(historyRanking.Score);
        explained.RankingValue.Should().Be(historyRanking.RankingValue);
        prepared.Single().Should().NotBeSameAs(mentor);
        mentor.Experience.Should().Be("General mentoring background");
        db.ChangeTracker.HasChanges().Should().BeFalse();
        await db.SaveChangesAsync();
        (await db.MentorProfiles.AsNoTracking().SingleAsync()).Experience.Should().Be("General mentoring background");
    }

    [Fact]
    public void HistoryCountsCannotInventRelevanceOrRaiseTheMatchingScore()
    {
        var mentor = new MentorProfile { Type = MentorType.Academic };
        var history = new MentorHistoryEvidence(100, 20, 5, "");
        var prepared = MentorHistoryEvidenceLoader.ForRanking([mentor], new Dictionary<Guid, MentorHistoryEvidence> { [mentor.Id] = history });
        var ranking = new FileMentorFitRanker(new ConfigurationBuilder().Build())
            .Rank("AI và computer vision", prepared, default)[mentor.Id];

        ranking.HasEvidence.Should().BeFalse();
        MentorHistoryEvidenceLoader.AddReasons(ranking, history).Should().Be(ranking);
        var relevant = new MentorFitRanking(70, ["Matching skill"], true, 70.4);
        var explained = MentorHistoryEvidenceLoader.AddReasons(relevant, history);
        explained.Score.Should().Be(70);
        explained.RankingValue.Should().Be(70.4);
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static MentorProfile AddMentor(AppDbContext db)
    {
        var user = new User { FullName = "Synthetic mentor", Email = $"{Guid.NewGuid():N}@example.test" };
        var mentor = new MentorProfile { User = user, UserId = user.Id };
        db.MentorProfiles.Add(mentor);
        return mentor;
    }

    private static MentorAssignment AddAssignment(AppDbContext db, MentorProfile mentor)
    {
        var team = new Team { TeamCode = Guid.NewGuid().ToString("N"), TeamName = "Synthetic team",
            Class = new Class { ClassCode = Guid.NewGuid().ToString("N"),
                Semester = new Semester { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic semester", Year = 2026 },
                Course = new Course { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic course" } } };
        var assignment = new MentorAssignment { MentorProfile = mentor, Team = team,
            AssignedAt = Now.AddDays(-10), Status = MentorAssignmentStatus.Active };
        db.MentorAssignments.Add(assignment);
        return assignment;
    }

    private static MentoringSession AddSession(AppDbContext db, MentorAssignment assignment, string title,
        DateTime start, MentoringSessionStatus status = MentoringSessionStatus.Completed)
    {
        var session = new MentoringSession { MentorAssignment = assignment, Title = title,
            StartAt = new DateTimeOffset(start), EndAt = new DateTimeOffset(start.AddHours(1)), Status = status };
        db.MentoringSessions.Add(session);
        return session;
    }

    private static ProjectActivityLog AddAchievementLog(AppDbContext db, Project project, DateTime occurredAt,
        params string[] tokens)
    {
        var activity = new ProjectActivityLog
        {
            Project = project, ProjectId = project.Id, Action = "PROJECT_ACHIEVEMENTS_CHANGED",
            Summary = "Achievement change", ChangedFieldsJson = JsonSerializer.Serialize(tokens), OccurredAtUtc = occurredAt
        };
        db.ProjectActivityLogs.Add(activity);
        return activity;
    }
}
