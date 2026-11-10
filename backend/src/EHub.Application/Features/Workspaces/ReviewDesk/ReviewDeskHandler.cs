using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.ReviewDesk;

public interface IReviewDeskHandler
{
    /// <summary>False when AI summaries are off for this environment; the review desk then does not exist for anyone.</summary>
    bool Enabled { get; }

    Task<Result<WorkspaceReviewDeskResponse>> GetDeskAsync(Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>Records that the reviewer has opened the team's newest version, so it stops counting as new for them.</summary>
    Task<Result> MarkSeenAsync(Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default);
}

/// <summary>
/// The reviewer's starting point: which teams need a look today. Admins see every class, a lecturer the classes they teach, a
/// mentor only the teams they mentor; a team outside that is answered as "no access", never as "not found", so an id reveals
/// nothing. The whole feature exists only while AI summaries are on.
/// </summary>
public sealed class ReviewDeskHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    CheckpointSummarySettings settings) : IReviewDeskHandler
{
    // A first-time reader has opened nothing yet; only recent submissions count as new for them.
    private static readonly TimeSpan NewWindow = TimeSpan.FromDays(14);
    private static readonly TimeSpan OverdueWindow = TimeSpan.FromDays(14);
    private const int MaximumInboxItems = 50;

    public bool Enabled => settings.Enabled;

    public async Task<Result<WorkspaceReviewDeskResponse>> GetDeskAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled) return Result.Failure<WorkspaceReviewDeskResponse>(ErrorCodes.WorkspaceSummaryDisabled, "The review desk is not available.");
        var scope = ReviewableTeams(userId, role);
        if (scope is null) return Result.Failure<WorkspaceReviewDeskResponse>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to the review desk.");

        var teams = await scope
            .Select(team => new { team.Id, team.TeamCode, team.TeamName, team.ClassId, ClassCode = team.Class.ClassCode })
            .ToListAsync(cancellationToken);
        if (teams.Count == 0) return Result.Success(new WorkspaceReviewDeskResponse());

        var now = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
        var classIds = teams.Select(team => team.ClassId).Distinct().ToArray();
        var schedules = await context.ClassCheckpointSchedules.AsNoTracking()
            .Where(item => classIds.Contains(item.ClassId) && item.Checkpoint.ClassId == null && item.Checkpoint.Status != CheckpointStatus.Archived)
            .Select(item => new
            {
                item.ClassId, item.CheckpointId, item.StartDateUtc, item.EndDateUtc,
                Number = item.Checkpoint.CheckpointNumber
            })
            .ToListAsync(cancellationToken);
        // Checkpoints worth looking at today: open now, or closed within the last two weeks.
        var active = schedules.Where(item => item.StartDateUtc <= now && item.EndDateUtc >= now - OverdueWindow).ToArray();
        var teamIds = teams.Select(team => team.Id).ToArray();
        var checkpointIds = active.Select(item => item.CheckpointId).Distinct().ToArray();
        var latest = await LatestSubmissionsAsync(teamIds, checkpointIds, cancellationToken);
        var markers = await context.CheckpointReviewMarkers.AsNoTracking()
            .Where(item => item.UserId == userId && teamIds.Contains(item.TeamId))
            .ToDictionaryAsync(item => (item.TeamId, item.CheckpointId), item => item.SeenVersionNumber, cancellationToken);

        var inbox = new List<(int Priority, DateTime SortAt, ReviewDeskInboxItemDto Item)>();
        foreach (var team in teams)
        foreach (var schedule in active.Where(item => item.ClassId == team.ClassId))
        {
            latest.TryGetValue((team.Id, schedule.CheckpointId), out var submission);
            ReviewDeskInboxItemDto Item(string reason) => new()
            {
                TeamId = team.Id, TeamCode = team.TeamCode, TeamName = team.TeamName, ClassId = team.ClassId, ClassCode = team.ClassCode,
                CheckpointNumber = schedule.Number, Reason = reason, VersionNumber = submission?.Version,
                SubmittedAtUtc = submission?.At, DueAtUtc = schedule.EndDateUtc
            };
            if (submission is null)
            {
                if (schedule.EndDateUtc < now) inbox.Add((0, schedule.EndDateUtc, Item(ReviewAttention.Overdue)));
                else if (schedule.EndDateUtc - now <= ReviewAttention.DueSoonWindow) inbox.Add((2, schedule.EndDateUtc, Item(ReviewAttention.DueSoon)));
            }
            else if (IsNew(markers, team.Id, schedule.CheckpointId, submission, now))
            {
                inbox.Add((1, submission.At, Item(ReviewAttention.NewVersion)));
            }
        }

        return Result.Success(new WorkspaceReviewDeskResponse
        {
            // Overdue first (longest overdue first), then new versions (newest first), then deadlines coming up (soonest first).
            Inbox = inbox.OrderBy(entry => entry.Priority)
                .ThenBy(entry => entry.Priority == 1 ? -entry.SortAt.Ticks : entry.SortAt.Ticks)
                .Take(MaximumInboxItems).Select(entry => entry.Item).ToArray()
        });
    }

    public async Task<Result> MarkSeenAsync(Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled) return Result.Failure(ErrorCodes.WorkspaceSummaryDisabled, "The review desk is not available.");
        // A student can reach their own team's workspace but has no review marker; only staff do.
        if (!IsReviewer(role)) return Result.Failure(ErrorCodes.WorkspaceAccessDenied, "You do not have access to this team workspace.");
        var access = await CheckpointWorkspaceAccess.ResolveAsync(context, teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure(access.Error);
        var checkpointId = access.Value.Checkpoint.Id;
        var newest = await context.Submissions.AsNoTracking()
            .Where(item => item.TeamId == teamId && item.CheckpointId == checkpointId && item.SubmittedAt != null)
            .MaxAsync(item => (int?)item.VersionNumber, cancellationToken);
        if (newest is null) return Result.Success();

        var now = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
        var marker = await context.CheckpointReviewMarkers
            .FirstOrDefaultAsync(item => item.UserId == userId && item.TeamId == teamId && item.CheckpointId == checkpointId, cancellationToken);
        if (marker is null)
        {
            context.CheckpointReviewMarkers.Add(new CheckpointReviewMarker
            {
                UserId = userId, TeamId = teamId, CheckpointId = checkpointId, SeenVersionNumber = newest.Value, SeenAtUtc = now,
                CreatedAt = now, CreatedBy = userId
            });
        }
        else if (marker.SeenVersionNumber < newest.Value)
        {
            marker.SeenVersionNumber = newest.Value;
            marker.SeenAtUtc = now;
            marker.UpdatedAt = now;
            marker.UpdatedBy = userId;
        }
        else return Result.Success();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two tabs recorded the first visit at once; either row says the same thing.
            context.ClearChanges();
        }
        return Result.Success();
    }

    private async Task<Dictionary<(Guid TeamId, Guid CheckpointId), LatestSubmission>> LatestSubmissionsAsync(
        Guid[] teamIds, Guid[] checkpointIds, CancellationToken cancellationToken)
    {
        var rows = await context.Submissions.AsNoTracking()
            .Where(item => teamIds.Contains(item.TeamId) && checkpointIds.Contains(item.CheckpointId) && item.SubmittedAt != null)
            .GroupBy(item => new { item.TeamId, item.CheckpointId })
            .Select(group => new { group.Key.TeamId, group.Key.CheckpointId, Version = group.Max(item => item.VersionNumber), At = group.Max(item => item.SubmittedAt)!.Value })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => (row.TeamId, row.CheckpointId), row => new LatestSubmission(row.Version, row.At));
    }

    private static bool IsNew(IReadOnlyDictionary<(Guid TeamId, Guid CheckpointId), int> markers, Guid teamId, Guid checkpointId,
        LatestSubmission submission, DateTime now) =>
        IsNew(markers.TryGetValue((teamId, checkpointId), out var seen) ? seen : null, submission.Version, submission.At, now);

    /// <summary>
    /// New when the reviewer has not opened this version. Someone who has opened nothing yet is told only about the last
    /// two weeks, so a first visit is not a list of every old submission.
    /// </summary>
    private static bool IsNew(int? seenVersion, int version, DateTime submittedAt, DateTime now) =>
        seenVersion is { } seen ? seen < version : now - submittedAt <= NewWindow;

    private IQueryable<Team>? ReviewableTeams(Guid userId, string role)
    {
        var teams = context.Teams.AsNoTracking().Where(team => team.Status == TeamStatus.Active && team.Class.Status != ClassStatus.Archived);
        if (IsRole(role, SystemRoles.Admin)) return teams;
        if (IsRole(role, SystemRoles.Lecturer))
            return teams.Where(team => team.Class.PrimaryLecturerId == userId || team.Class.ClassLecturers.Any(item => item.LecturerId == userId));
        if (IsRole(role, SystemRoles.Mentor))
            return teams.Where(team => team.MentorAssignments.Any(assignment =>
                assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null && assignment.MentorProfile.UserId == userId));
        return null;
    }

    private static bool IsReviewer(string role) =>
        IsRole(role, SystemRoles.Admin) || IsRole(role, SystemRoles.Lecturer) || IsRole(role, SystemRoles.Mentor);

    private static bool IsRole(string role, string expected) => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase);

    private sealed record LatestSubmission(int Version, DateTime At);
}
