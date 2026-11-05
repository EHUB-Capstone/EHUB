using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.ProjectData.ManageCompetitions;

/// <summary>
/// Replaces the competitions of a project. Administrators may change any project; a lecturer only the projects of
/// classes assigned to them. The project's row version guards against two people saving over each other.
/// </summary>
public sealed class UpdateProjectCompetitionsCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IUpdateProjectCompetitionsCommandHandler
{
    private const int SummaryMaxLength = 300;

    private readonly record struct EntryKey(string Competition, string NormalizedOtherName, int Year);

    private sealed record WantedEntry(string Competition, string? OtherName, string NormalizedOtherName, int Year, CompetitionStage Stage)
    {
        internal EntryKey Key => new(Competition, NormalizedOtherName, Year);
    }

    public async Task<Result<ProjectCompetitionsResponse>> HandleAsync(
        Guid projectId,
        UpdateProjectCompetitionsRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = ProjectDataQuery.IsAdmin(currentUserRole);
        if (!isAdmin && !ProjectDataQuery.IsLecturer(currentUserRole))
            return Failure(ErrorCodes.ProjectDataAccessDenied, "Only administrators and lecturers can update project competitions.");

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
            return Failure(ErrorCodes.ProjectDataValidationError, "A valid rowVersion is required.");

        var parsed = Parse(request.Entries ?? []);
        if (parsed.Error is not null) return Failure(ErrorCodes.ProjectDataValidationError, parsed.Error);
        var wanted = parsed.Entries;

        // The project must exist within the feature scope (administrator view) before role-specific access is judged.
        var project = await ProjectDataQuery.Scoped(
                context.Projects.Include(item => item.ProjectCompetitionEntries), currentUserId, isAdmin: true)
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

        var current = project.ProjectCompetitionEntries.ToDictionary(
            entry => new EntryKey(entry.Competition, entry.NormalizedOtherName, entry.Year));
        var wantedByKey = wanted.ToDictionary(entry => entry.Key);

        var removed = current.Where(pair => !wantedByKey.ContainsKey(pair.Key)).Select(pair => pair.Value).ToArray();
        var added = wanted.Where(entry => !current.ContainsKey(entry.Key)).ToArray();
        var changed = wanted
            .Where(entry => current.TryGetValue(entry.Key, out var existing) && existing.Stage != entry.Stage)
            .ToArray();

        // An unchanged list is a no-op so retries after a lost response stay harmless.
        if (removed.Length == 0 && added.Length == 0 && changed.Length == 0)
            return Result.Success(ToResponse(project));

        var now = dateTimeProvider.UtcNow;
        var summaryParts = new List<string>();
        var tokens = new List<string>();

        foreach (var entry in removed)
        {
            context.ProjectCompetitionEntries.Remove(entry);
            project.ProjectCompetitionEntries.Remove(entry);
            summaryParts.Add($"removed {ProjectCompetitionMapping.Describe(entry)}");
            tokens.Add("-" + Token(entry));
        }

        foreach (var entry in changed)
        {
            var existing = current[entry.Key];
            summaryParts.Add($"{ProjectCompetitionMapping.DisplayName(existing.Competition, existing.OtherName)} {existing.Year}: {existing.Stage} to {entry.Stage}");
            existing.Stage = entry.Stage;
            existing.UpdatedAt = now;
            existing.UpdatedById = currentUserId;
            tokens.Add("~" + Token(existing));
        }

        foreach (var entry in added)
        {
            var created = new ProjectCompetitionEntry
            {
                ProjectId = project.Id,
                Project = project,
                Competition = entry.Competition,
                OtherName = entry.OtherName,
                NormalizedOtherName = entry.NormalizedOtherName,
                Year = entry.Year,
                Stage = entry.Stage,
                CreatedById = currentUserId,
                CreatedAt = now,
            };
            context.ProjectCompetitionEntries.Add(created);
            summaryParts.Add($"added {ProjectCompetitionMapping.Describe(created)}");
            tokens.Add("+" + Token(created));
        }

        // Entries live in their own table, so the project row is touched to move its version and record who changed it.
        project.UpdatedAt = now;
        project.UpdatedBy = currentUserId;

        context.ProjectActivityLogs.Add(new ProjectActivityLog
        {
            ProjectId = project.Id,
            ActorUserId = currentUserId,
            Action = ProjectCompetitionMapping.ChangedAction,
            Summary = BuildSummary(summaryParts),
            ChangedFieldsJson = JsonSerializer.Serialize(tokens),
            OccurredAtUtc = now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The project row is touched by every save, so two saves racing on the same project conflict here.
            return Failure(ErrorCodes.ProjectDataConcurrencyConflict, "The project was changed by another user. Refresh and try again.");
        }

        return Result.Success(ToResponse(project));
    }

    private static (WantedEntry[] Entries, string? Error) Parse(IReadOnlyCollection<ProjectCompetitionEntryRequest> entries)
    {
        if (entries.Count > ProjectCompetitionNames.MaxEntriesPerProject)
            return ([], $"A project can have at most {ProjectCompetitionNames.MaxEntriesPerProject} competitions.");

        var result = new List<WantedEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (ProjectCompetitionMapping.Canonicalize(entry.Competition) is not { } competition)
                return ([], "Competition must be one of the listed competitions.");
            if (ProjectCompetitionMapping.ParseStage(entry.Stage) is not { } stage)
                return ([], $"Stage must be {string.Join(", ", ProjectCompetitionStageNames.All)}.");
            if (entry.Year is < ProjectCompetitionNames.MinYear or > ProjectCompetitionNames.MaxYear)
                return ([], $"The year must be between {ProjectCompetitionNames.MinYear} and {ProjectCompetitionNames.MaxYear}.");

            string? otherName = null;
            if (ProjectCompetitionMapping.IsOthers(competition))
            {
                otherName = ProjectCompetitionMapping.CleanOtherName(entry.OtherName);
                if (otherName is null)
                    return ([], "Enter the competition name when you choose Others.");
                if (otherName.Length > ProjectCompetitionNames.OtherNameMaxLength)
                    return ([], $"The competition name must be at most {ProjectCompetitionNames.OtherNameMaxLength} characters.");
            }

            result.Add(new WantedEntry(
                competition, otherName, ProjectCompetitionMapping.NormalizeOtherName(otherName), entry.Year, stage));
        }

        var duplicate = result.GroupBy(entry => entry.Key).FirstOrDefault(group => group.Count() > 1);
        return duplicate is null
            ? ([.. result], null)
            : ([], $"{ProjectCompetitionMapping.DisplayName(duplicate.First().Competition, duplicate.First().OtherName)} {duplicate.Key.Year} is listed more than once.");
    }

    private static ProjectCompetitionsResponse ToResponse(Project project) => new()
    {
        ProjectId = project.Id,
        Competitions = ProjectCompetitionMapping.ToOrderedResponses(project.ProjectCompetitionEntries),
        RowVersion = project.Version.ToString(),
    };

    private static string Token(ProjectCompetitionEntry entry) =>
        $"{ProjectCompetitionMapping.DisplayName(entry.Competition, entry.OtherName)}|{entry.Year}|{entry.Stage}";

    /// <summary>One line for the activity log; cut so the summary always fits its column.</summary>
    private static string BuildSummary(IReadOnlyCollection<string> parts)
    {
        var summary = $"Competitions changed: {string.Join("; ", parts)}.";
        return summary.Length <= SummaryMaxLength ? summary : summary[..(SummaryMaxLength - 1)] + "…";
    }

    private static Result<ProjectCompetitionsResponse> Failure(string code, string message) =>
        Result.Failure<ProjectCompetitionsResponse>(code, message);
}
