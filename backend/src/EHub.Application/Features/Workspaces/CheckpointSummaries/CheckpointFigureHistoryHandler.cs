using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Contracts.Workspaces;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public interface ICheckpointFigureHistoryHandler
{
    /// <summary>Figures of this checkpoint that disagree with what the team wrote in its earlier checkpoints. Not available while AI summaries are off.</summary>
    Task<Result<WorkspaceCheckpointFigureHistoryResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default);
}

/// <summary>
/// Checks the figures a team states now against the ones in its earlier checkpoints (a survey of 51 people in Checkpoint 2
/// and 52 in Checkpoint 3). It reuses the figures already taken from each file by the quick view and the summary, so no file
/// is downloaded or read again, and it needs no model. A file that has no figures yet is named rather than silently skipped.
/// </summary>
public sealed class CheckpointFigureHistoryHandler(
    IApplicationDbContext context,
    CheckpointSummarySettings settings) : ICheckpointFigureHistoryHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // A checkpoint rarely has more files than this; the cap keeps a pathological team from loading everything.
    private const int MaximumFiles = 60;
    private const int MaximumConflicts = 6;

    public async Task<Result<WorkspaceCheckpointFigureHistoryResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return Result.Failure<WorkspaceCheckpointFigureHistoryResponse>(ErrorCodes.WorkspaceSummaryDisabled, "AI summaries are not enabled on this server.");
        if (CheckpointWorkspaceAccess.IsStudent(role) || !IsReviewer(role))
            return Result.Failure<WorkspaceCheckpointFigureHistoryResponse>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to AI summaries for this team.");
        var access = await CheckpointWorkspaceAccess.ResolveAsync(context, teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointFigureHistoryResponse>(access.Error);

        // The newest version of each file, of this checkpoint and of the earlier ones.
        var rows = await context.SubmissionFiles.AsNoTracking()
            .Where(file => file.Submission.TeamId == teamId && file.Submission.Checkpoint.CheckpointNumber <= checkpointNumber)
            .Select(file => new FileRow(file.Id, file.OriginalName, file.VersionNumber, file.UploadedAt, file.Submission.Checkpoint.CheckpointNumber))
            .ToListAsync(cancellationToken);
        var newest = rows.GroupBy(row => (row.Number, Name: row.Name.Trim().ToLowerInvariant()))
            .Select(group => group.OrderByDescending(row => row.Version).ThenByDescending(row => row.UploadedAt).First())
            .OrderBy(row => row.Number).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumFiles)
            .ToArray();
        var earlierNumbers = newest.Where(row => row.Number < checkpointNumber).Select(row => row.Number).Distinct().OrderBy(number => number).ToArray();
        if (earlierNumbers.Length == 0 || newest.All(row => row.Number != checkpointNumber))
            return Result.Success(new WorkspaceCheckpointFigureHistoryResponse { CheckpointNumber = checkpointNumber });

        var ids = newest.Select(row => row.Id).ToArray();
        var summaries = await context.SubmissionFileSummaries.AsNoTracking()
            .Where(summary => ids.Contains(summary.SubmissionFileId))
            .Select(summary => new { summary.SubmissionFileId, summary.Status, summary.PreviewJson, summary.ContentJson })
            .ToDictionaryAsync(summary => summary.SubmissionFileId, cancellationToken);

        var files = new List<(string Label, IReadOnlyCollection<string> Figures)>();
        var numberOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var unchecked_ = new List<string>();
        foreach (var row in newest)
        {
            var label = $"CP{row.Number} · {row.Name}";
            summaries.TryGetValue(row.Id, out var summary);
            var content = summary is { Status: CheckpointSummaryStatus.Ready } ? Read<WorkspaceFileSummaryContent>(summary.ContentJson) : null;
            var figures = content?.Figures ?? (summary is null ? null : Read<WorkspaceFilePreview>(summary.PreviewJson)?.Figures);
            if (figures is null)
            {
                // Only earlier files matter to the reviewer here; the current checkpoint's own files are shown elsewhere.
                if (row.Number < checkpointNumber) unchecked_.Add(label);
                continue;
            }
            if (figures.Count == 0) continue;
            files.Add((label, figures));
            numberOf[label] = row.Number;
        }

        // This checkpoint against each earlier one on its own: a comparison of everything at once keeps only one file per distinct
        // value and could drop the very file of this checkpoint. Two earlier checkpoints that disagree with each other were this
        // team's problem when the later one was reviewed, so they are not repeated, and neither are conflicts inside this checkpoint.
        var current = files.Where(file => numberOf[file.Label] == checkpointNumber).ToArray();
        var conflicts = new List<WorkspaceFigureConflict>();
        foreach (var earlier in earlierNumbers)
        {
            var pair = current.Concat(files.Where(file => numberOf[file.Label] == earlier)).Select(file => (file.Label, file.Figures)).ToArray();
            conflicts.AddRange(CheckpointCrossFileFigures.Compare(pair).Where(conflict =>
                conflict.Values.Any(value => value.Source is { } source && numberOf.GetValueOrDefault(source) == checkpointNumber) &&
                conflict.Values.Any(value => value.Source is { } source && numberOf.GetValueOrDefault(source) == earlier)));
        }

        return Result.Success(new WorkspaceCheckpointFigureHistoryResponse
        {
            CheckpointNumber = checkpointNumber, ComparedCheckpoints = earlierNumbers, Conflicts = conflicts.Take(MaximumConflicts).ToArray(), UncheckedFiles = unchecked_
        });
    }

    private static T? Read<T>(string? json) where T : class
    {
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<T>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static bool IsReviewer(string role) =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase);

    private sealed record FileRow(Guid Id, string Name, int Version, DateTime UploadedAt, int Number);
}
