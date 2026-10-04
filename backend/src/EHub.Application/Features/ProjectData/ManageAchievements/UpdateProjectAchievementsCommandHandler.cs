using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.ManageAchievements;

public sealed class UpdateProjectAchievementsCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IUpdateProjectAchievementsCommandHandler
{
    private const int SummaryMaxLength = 300;

    public async Task<Result<ProjectAchievementsResponse>> HandleAsync(
        Guid projectId,
        UpdateProjectAchievementsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
        {
            return Failure(ErrorCodes.ProjectDataAccessDenied, "Only administrators and lecturers can update project achievements.");
        }

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
            return Failure(ErrorCodes.ProjectDataValidationError, "A valid rowVersion is required.");

        var requested = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in request.Achievements ?? [])
        {
            if (ProjectAchievementMapping.Canonicalize(value) is not { } canonical)
                return Failure(ErrorCodes.ProjectDataValidationError, "Achievement must be Potential, Funded or Awarded.");
            requested.Add(canonical);
        }

        var note = ProjectAchievementMapping.NormalizeNote(request.Note);
        if (note is { Length: > ProjectAchievementMapping.NoteMaxLength })
            return Failure(ErrorCodes.ProjectDataValidationError, $"The note must be at most {ProjectAchievementMapping.NoteMaxLength} characters.");
        if (note is not null && requested.Count == 0)
            return Failure(ErrorCodes.ProjectDataValidationError, "A note needs at least one achievement.");

        // The project must exist within the feature scope (administrator view) before role-specific access is judged.
        var project = await ProjectDataQuery.Scoped(context.Projects, currentUserId, isAdmin: true)
            .FirstOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
            return Failure(ErrorCodes.ProjectDataNotFound, "The project was not found.");

        if (!isAdmin && !await ProjectDataQuery.Scoped(context.Projects.AsNoTracking(), currentUserId, isAdmin: false)
                .AnyAsync(item => item.Id == projectId, cancellationToken))
        {
            return Failure(ErrorCodes.ProjectDataAccessDenied, "You can only update projects of classes assigned to you.");
        }

        if (project.Version != expectedVersion)
            return Failure(ErrorCodes.ProjectDataConcurrencyConflict, "The project was changed by another user. Refresh and try again.");

        var before = ProjectAchievementMapping.ToNames(project);
        var nextPotential = requested.Contains(ProjectAchievementNames.Potential);
        var nextFunded = requested.Contains(ProjectAchievementNames.Funded);
        var nextAwarded = requested.Contains(ProjectAchievementNames.Awarded);

        var labelsChanged = project.IsHighPotential != nextPotential ||
                            project.IsFunded != nextFunded ||
                            project.IsAwarded != nextAwarded;
        var noteChanged = !string.Equals(project.AchievementNote, note, StringComparison.Ordinal);

        // An unchanged label set and note is a no-op so retries after a lost response stay harmless.
        if (!labelsChanged && !noteChanged)
            return Result.Success(await ToResponseAsync(project, cancellationToken));

        var now = dateTimeProvider.UtcNow;
        project.IsHighPotential = nextPotential;
        project.IsFunded = nextFunded;
        project.IsAwarded = nextAwarded;
        project.AchievementNote = note;
        project.AchievementsUpdatedAt = now;
        project.AchievementsUpdatedBy = currentUserId;
        project.UpdatedAt = now;
        project.UpdatedBy = currentUserId;

        var after = ProjectAchievementMapping.ToNames(project);
        var changedFields = ProjectAchievementHistoryParser.BuildTokens(before, after, noteChanged, note);
        context.ProjectActivityLogs.Add(new ProjectActivityLog
        {
            ProjectId = project.Id,
            ActorUserId = currentUserId,
            Action = ProjectAchievementMapping.ChangedAction,
            Summary = BuildSummary(labelsChanged, before, after, note),
            ChangedFieldsJson = JsonSerializer.Serialize(changedFields),
            OccurredAtUtc = now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ErrorCodes.ProjectDataConcurrencyConflict, "The project was changed by another user. Refresh and try again.");
        }

        return Result.Success(await ToResponseAsync(project, cancellationToken));
    }

    private async Task<ProjectAchievementsResponse> ToResponseAsync(Project project, CancellationToken cancellationToken)
    {
        ProjectDataPersonResponse? updatedBy = null;
        if (project.AchievementsUpdatedBy is { } updaterId)
        {
            var name = await context.Users.AsNoTracking()
                .Where(user => user.Id == updaterId)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            if (name is not null)
                updatedBy = new ProjectDataPersonResponse { UserId = updaterId, FullName = name };
        }

        return new ProjectAchievementsResponse
        {
            ProjectId = project.Id,
            Achievements = ProjectAchievementMapping.ToNames(project),
            RowVersion = project.Version.ToString(),
            Note = project.AchievementNote,
            UpdatedAtUtc = project.AchievementsUpdatedAt,
            UpdatedBy = updatedBy,
        };
    }

    /// <summary>One line for the activity log; the note is cut so the summary always fits its column.</summary>
    private static string BuildSummary(
        bool labelsChanged,
        IReadOnlyCollection<string> before,
        IReadOnlyCollection<string> after,
        string? note)
    {
        var summary = labelsChanged
            ? $"Achievements changed from {Describe(before)} to {Describe(after)}."
            : "Achievement note updated.";
        if (note is null) return summary;

        const string prefix = " Note: \"";
        var room = SummaryMaxLength - summary.Length - prefix.Length - 1;
        if (room <= 0) return summary;
        var shown = note.Length <= room ? note : note[..Math.Max(0, room - 1)] + "…";
        return $"{summary}{prefix}{shown}\"";
    }

    private static string Describe(IReadOnlyCollection<string> names) =>
        names.Count == 0 ? "none" : string.Join(", ", names);

    private static Result<ProjectAchievementsResponse> Failure(string code, string message) =>
        Result.Failure<ProjectAchievementsResponse>(code, message);
}
