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

public interface ICheckpointProgressHandler
{
    /// <summary>
    /// What each checkpoint of the team mentions of the things the rubrics ask for, up to this checkpoint. Not available while
    /// AI summaries are off.
    /// </summary>
    Task<Result<WorkspaceCheckpointProgressResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the requirement findings already stored for the newest version of each file (the summary when it is ready, otherwise
/// the quick view), so nothing is downloaded or read again and no model is called. A file with nothing stored is counted as not
/// read, so a thing is never called "not mentioned" on the strength of a file nobody looked at.
/// </summary>
public sealed class CheckpointProgressHandler(
    IApplicationDbContext context,
    CheckpointSummarySettings settings) : ICheckpointProgressHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // A checkpoint rarely has more files than this; the cap keeps a pathological team from loading everything.
    private const int MaximumFiles = 60;

    public async Task<Result<WorkspaceCheckpointProgressResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return Result.Failure<WorkspaceCheckpointProgressResponse>(ErrorCodes.WorkspaceSummaryDisabled, "AI summaries are not enabled on this server.");
        if (CheckpointWorkspaceAccess.IsStudent(role) || !IsReviewer(role))
            return Result.Failure<WorkspaceCheckpointProgressResponse>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to AI summaries for this team.");
        var access = await CheckpointWorkspaceAccess.ResolveAsync(context, teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointProgressResponse>(access.Error);

        var rows = await context.SubmissionFiles.AsNoTracking()
            .Where(file => file.Submission.TeamId == teamId && file.Submission.Checkpoint.CheckpointNumber <= checkpointNumber)
            .Select(file => new FileRow(file.Id, file.OriginalName, file.VersionNumber, file.UploadedAt, file.Submission.Checkpoint.CheckpointNumber))
            .ToListAsync(cancellationToken);
        var newest = rows.GroupBy(row => (row.Number, Name: row.Name.Trim().ToLowerInvariant()))
            .Select(group => group.OrderByDescending(row => row.Version).ThenByDescending(row => row.UploadedAt).First())
            .OrderBy(row => row.Number).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumFiles)
            .ToArray();
        if (newest.Select(row => row.Number).Distinct().Count() < 2 || newest.All(row => row.Number != checkpointNumber))
            return Result.Success(new WorkspaceCheckpointProgressResponse { CheckpointNumber = checkpointNumber });

        var ids = newest.Select(row => row.Id).ToArray();
        var summaries = await context.SubmissionFileSummaries.AsNoTracking()
            .Where(summary => ids.Contains(summary.SubmissionFileId))
            .Select(summary => new { summary.SubmissionFileId, summary.Status, summary.PreviewJson, summary.ContentJson })
            .ToDictionaryAsync(summary => summary.SubmissionFileId, cancellationToken);

        var checkpoints = newest.GroupBy(row => row.Number).Select(group =>
        {
            var read = new List<IReadOnlyCollection<WorkspaceFileRequirementFinding>>();
            foreach (var row in group)
            {
                if (!summaries.TryGetValue(row.Id, out var summary)) continue;
                var requirements = summary.Status == CheckpointSummaryStatus.Ready ? Read<WorkspaceFileSummaryContent>(summary.ContentJson)?.Requirements : null;
                requirements ??= Read<WorkspaceFilePreview>(summary.PreviewJson)?.Requirements;
                if (requirements is not null) read.Add(requirements);
            }
            return new CheckpointCoverageProgress.CheckpointInput(group.Key, group.Count(), read);
        }).ToArray();

        return Result.Success(CheckpointCoverageProgress.Build(checkpointNumber, checkpoints));
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
