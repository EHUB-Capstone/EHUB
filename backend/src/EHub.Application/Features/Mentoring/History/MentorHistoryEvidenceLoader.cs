using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.ProjectData.Common;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Mentoring.History;

// Evidence text stays inside matching. API responses expose the aggregate facts, never other teams' notes.
public sealed record MentorHistoryEvidence(
    int CompletedSessionCount, int MentoredTeamCount, int AchievementCount, string EvidenceText);

public static class MentorHistoryEvidenceLoader
{
    private const int MentorBatchSize = 100;
    private const int MaximumRecentSessions = 16;
    private const int MaximumRecentAchievements = 8;
    private const int MaximumHistoryCharacters = 3000;

    public static async Task<IReadOnlyDictionary<Guid, MentorHistoryEvidence>> LoadAsync(
        IApplicationDbContext context, IReadOnlyCollection<Guid> mentorIds, DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, MentorHistoryEvidence>();
        var now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
        // A scheduled/cancelled/future session is not evidence of coaching. Also reject sessions outside
        // the actual assignment interval, including imported records with inconsistent dates.
        var completed = context.MentoringSessions.AsNoTracking().Where(session =>
            session.Status == MentoringSessionStatus.Completed && session.EndAt <= now &&
            session.EndAt >= session.StartAt && session.StartAt.UtcDateTime >= session.MentorAssignment.AssignedAt &&
            (session.MentorAssignment.EndedAt == null || session.EndAt.UtcDateTime <= session.MentorAssignment.EndedAt) &&
            (session.MentorAssignment.Status == MentorAssignmentStatus.Active ||
             session.MentorAssignment.Status == MentorAssignmentStatus.Ended));
        foreach (var batch in mentorIds.Distinct().Chunk(MentorBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var achievements = await LoadAchievementsAsync(context, batch, utcNow, cancellationToken);
            // Correlated, limited collections keep the materialized text bounded per mentor while
            // the counts are calculated in the database over the complete eligible history.
            var rows = await context.MentorProfiles.AsNoTracking().Where(profile => batch.Contains(profile.Id))
                .Select(profile => new
                {
                    profile.Id,
                    CompletedSessionCount = completed.Count(session => session.MentorAssignment.MentorProfileId == profile.Id),
                    MentoredTeamCount = completed.Where(session => session.MentorAssignment.MentorProfileId == profile.Id)
                        .Select(session => session.MentorAssignment.TeamId).Distinct().Count(),
                    Sessions = completed.Where(session => session.MentorAssignment.MentorProfileId == profile.Id)
                        .OrderByDescending(session => session.EndAt).ThenBy(session => session.Id)
                        .Take(MaximumRecentSessions)
                        .Select(session => new { session.Title, session.Notes }).ToList()
                }).ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                var sessionEvidence = row.Sessions.Select(session =>
                    Clip(session.Title, 200) + ". " + Clip(session.Notes, 700));
                var achievementHistory = achievements.GetValueOrDefault(row.Id);
                var achievementEvidence = achievementHistory?.RecentNotes.Select(item => item.Note) ?? [];
                var evidence = Clip(string.Join('\n', achievementEvidence.Where(text => !string.IsNullOrWhiteSpace(text))), 799) + "\n" +
                    Clip(string.Join('\n', sessionEvidence), 2200);
                result[row.Id] = new MentorHistoryEvidence(row.CompletedSessionCount,
                    row.MentoredTeamCount, achievementHistory?.ProjectIds.Count ?? 0, evidence);
            }
        }

        return result;
    }

    private static async Task<Dictionary<Guid, AchievementEvidence>> LoadAchievementsAsync(
        IApplicationDbContext context, Guid[] mentorIds, DateTime utcNow, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, AchievementEvidence>();
        var assignments = context.MentorAssignments.AsNoTracking().Where(assignment =>
            mentorIds.Contains(assignment.MentorProfileId) && assignment.ProjectId != null &&
            assignment.Project != null && assignment.Project.TeamId == assignment.TeamId &&
            (assignment.Status == MentorAssignmentStatus.Active || assignment.Status == MentorAssignmentStatus.Ended));

        // A note edit must neither erase the old mentor's achievement nor credit the new mentor.
        // Stream bounded event bodies, retaining only project ids and the latest eight notes per mentor.
        var events = from assignment in assignments
                     join activity in context.ProjectActivityLogs.AsNoTracking()
                         on assignment.ProjectId equals (Guid?)activity.ProjectId
                     where activity.Action == ProjectAchievementMapping.ChangedAction &&
                           activity.OccurredAtUtc >= assignment.AssignedAt && activity.OccurredAtUtc <= utcNow &&
                           (assignment.EndedAt == null || activity.OccurredAtUtc <= assignment.EndedAt)
                     orderby activity.OccurredAtUtc descending, activity.Id descending
                     select new
                     {
                         assignment.MentorProfileId, activity.ProjectId, activity.OccurredAtUtc, activity.Action,
                         Summary = activity.Summary.Length > 300 ? activity.Summary.Substring(0, 300) : activity.Summary,
                         Fields = activity.ChangedFieldsJson
                     };
        await foreach (var activity in events.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var change = ProjectAchievementHistoryParser.Parse(activity.Action, activity.Summary, Clip(activity.Fields, 4096));
            if (change.Added.Count == 0) continue;
            AddAchievement(result, activity.MentorProfileId, activity.ProjectId, activity.OccurredAtUtc, change.Note);
        }

        // Imports predating achievement activity logs retain their dated snapshot as a fallback.
        // Any achievement log, even a carried-over or future entry, prevents snapshot attribution.
        var legacy = assignments.Where(assignment =>
                !context.ProjectActivityLogs.Any(activity => activity.ProjectId == assignment.ProjectId &&
                    (activity.Action == ProjectAchievementMapping.ChangedAction || activity.Action == ProjectAchievementMapping.CarriedOverAction)) &&
                assignment.Project!.AchievementsUpdatedAt >= assignment.AssignedAt &&
                assignment.Project.AchievementsUpdatedAt <= utcNow &&
                (assignment.EndedAt == null || assignment.Project.AchievementsUpdatedAt <= assignment.EndedAt) &&
                (assignment.Project.IsAwarded || assignment.Project.IsInternallyFunded ||
                 assignment.Project.IsExternallyFunded || assignment.Project.IsHighPotential))
            .OrderByDescending(assignment => assignment.Project!.AchievementsUpdatedAt).ThenByDescending(assignment => assignment.Id)
            .Select(assignment => new
            {
                assignment.MentorProfileId, ProjectId = assignment.ProjectId!.Value,
                At = assignment.Project!.AchievementsUpdatedAt!.Value,
                Note = assignment.Project.AchievementNote != null && assignment.Project.AchievementNote.Length > 500
                    ? assignment.Project.AchievementNote.Substring(0, 500) : assignment.Project.AchievementNote
            });
        await foreach (var snapshot in legacy.AsAsyncEnumerable().WithCancellation(cancellationToken))
            AddAchievement(result, snapshot.MentorProfileId, snapshot.ProjectId, snapshot.At, snapshot.Note);
        return result;
    }

    private static void AddAchievement(Dictionary<Guid, AchievementEvidence> result, Guid mentorId,
        Guid projectId, DateTime occurredAt, string? note)
    {
        if (!result.TryGetValue(mentorId, out var evidence)) result[mentorId] = evidence = new AchievementEvidence();
        if (!evidence.ProjectIds.Add(projectId)) return;
        evidence.RecentNotes.Add(new AchievementNote(occurredAt, projectId, Clip(note, 500)));
        evidence.RecentNotes.Sort((left, right) =>
        {
            var byTime = right.At.CompareTo(left.At);
            return byTime != 0 ? byTime : right.ProjectId.CompareTo(left.ProjectId);
        });
        if (evidence.RecentNotes.Count > MaximumRecentAchievements)
            evidence.RecentNotes.RemoveAt(MaximumRecentAchievements);
    }

    private sealed class AchievementEvidence
    {
        public HashSet<Guid> ProjectIds { get; } = [];
        public List<AchievementNote> RecentNotes { get; } = [];
    }

    private sealed record AchievementNote(DateTime At, Guid ProjectId, string Note);

    // Copies contain only the fields consumed by matching. Never return these copies as profile DTOs,
    // persist them, or pass them to the semantic reranker, which can quote experience in its reasons.
    public static IReadOnlyCollection<MentorProfile> ForRanking(IReadOnlyCollection<MentorProfile> profiles,
        IReadOnlyDictionary<Guid, MentorHistoryEvidence> history) => profiles.Select(profile =>
    {
        if (!history.TryGetValue(profile.Id, out var evidence) || string.IsNullOrWhiteSpace(evidence.EvidenceText))
            return profile;
        var declaredExperience = Clip(profile.Experience, 4000);
        return new RankingProfile(profile.Id)
        {
            UserId = profile.UserId, User = profile.User, Type = profile.Type,
            Status = profile.Status, Expertise = profile.Expertise, StartupDomains = profile.StartupDomains,
            TechnologySkills = profile.TechnologySkills, MentorTags = profile.MentorTags,
            Experiences = profile.Experiences, Bio = profile.Bio,
            Experience = declaredExperience + "\n" +
                Clip(evidence.EvidenceText, Math.Min(MaximumHistoryCharacters, 5999 - declaredExperience.Length))
        };
    }).ToArray();

    private sealed class RankingProfile : MentorProfile
    {
        public RankingProfile(Guid id) => Id = id;
    }

    public static MentorFitRanking AddReasons(MentorFitRanking ranking, MentorHistoryEvidence evidence)
    {
        if (!ranking.HasEvidence) return ranking;
        var reasons = ranking.Reasons.ToList();
        if (evidence.CompletedSessionCount > 0)
            reasons.Add($"Lịch sử đã ghi nhận: {evidence.CompletedSessionCount} buổi hướng dẫn hoàn tất cho {evidence.MentoredTeamCount} nhóm");
        if (evidence.AchievementCount > 0)
            reasons.Add($"Có {evidence.AchievementCount} dự án ghi nhận thành tích trong thời gian phụ trách");
        return ranking with { Reasons = reasons.Distinct(StringComparer.Ordinal).ToArray() };
    }

    private static string Clip(string? text, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var length = Math.Min(text.Length, maximum);
        if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
        return text[..length];
    }
}
