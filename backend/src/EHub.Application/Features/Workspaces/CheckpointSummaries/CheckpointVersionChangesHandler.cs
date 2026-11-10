using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public interface ICheckpointVersionChangesHandler
{
    /// <summary>What changed in the files the team uploaded again, for a reviewer of the team. Not available while AI summaries are off.</summary>
    Task<Result<WorkspaceCheckpointVersionChangesResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default);
}

/// <summary>
/// Compares each file the team replaced with its previous version. The text of both versions is read again on request, so
/// nothing is stored and a reviewer always sees the current files. A file is matched to its earlier version by name.
/// </summary>
public sealed class CheckpointVersionChangesHandler(
    IApplicationDbContext context,
    CheckpointFileText fileText,
    CheckpointSummarySettings settings) : ICheckpointVersionChangesHandler
{
    // Reading two documents is the costly part; a checkpoint rarely has more files than this that were replaced.
    private const int MaximumComparedFiles = 5;
    private const int MaximumItemsPerFile = 8;

    public async Task<Result<WorkspaceCheckpointVersionChangesResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return Result.Failure<WorkspaceCheckpointVersionChangesResponse>(ErrorCodes.WorkspaceSummaryDisabled, "AI summaries are not enabled on this server.");
        // Like the summaries, this supports grading conversations; students are refused before any lookup.
        if (CheckpointWorkspaceAccess.IsStudent(role) || !IsReviewer(role))
            return Result.Failure<WorkspaceCheckpointVersionChangesResponse>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to AI summaries for this team.");
        var access = await CheckpointWorkspaceAccess.ResolveAsync(context, teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointVersionChangesResponse>(access.Error);

        var source = await CheckpointSummarySource.LoadAsync(context, teamId, access.Value.Checkpoint, cancellationToken);
        var results = new List<WorkspaceFileVersionChangeDto>();
        var groups = source.Files.GroupBy(file => file.OriginalName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(file => file.VersionNumber).ThenByDescending(file => file.UploadedAt).ToArray())
            .OrderByDescending(group => group[0].VersionNumber).ThenByDescending(group => group[0].UploadedAt)
            .ToArray();

        foreach (var versions in groups)
        {
            var current = versions[0];
            if (versions.Length == 1)
            {
                // A file that first appears after the first upload is new to the reviewer, whatever else was uploaded.
                if (current.VersionNumber > 1)
                    results.Add(new WorkspaceFileVersionChangeDto { FileId = current.Id, FileName = current.OriginalName, ToVersion = current.VersionNumber, Status = "NewFile" });
                continue;
            }
            if (results.Count(item => item.Status != "NewFile") >= MaximumComparedFiles) break;
            results.Add(await CompareAsync(versions[1], current, cancellationToken));
        }
        return Result.Success(new WorkspaceCheckpointVersionChangesResponse { CheckpointNumber = checkpointNumber, Files = results });
    }

    private async Task<WorkspaceFileVersionChangeDto> CompareAsync(SummarySourceFile previous, SummarySourceFile current, CancellationToken cancellationToken)
    {
        WorkspaceFileVersionChangeDto Outcome(string status, TextDiff? diff = null, int unchangedPercent = 0,
            IReadOnlyCollection<WorkspaceVersionChangeItemDto>? items = null) => new()
        {
            FileId = current.Id, FileName = current.OriginalName, FromVersion = previous.VersionNumber, ToVersion = current.VersionNumber, Status = status,
            AddedCount = diff?.Added.Count ?? 0, RemovedCount = diff?.Removed.Count ?? 0, ChangedCount = diff?.Changed.Count ?? 0,
            UnchangedPercent = unchangedPercent, Items = items ?? []
        };

        var before = await fileText.ReadAsync(previous.Id, cancellationToken);
        var after = await fileText.ReadAsync(current.Id, cancellationToken);
        // One of the versions cannot be read (scanned, protected, removed from storage): say so rather than compare half.
        if (before.IsFailure || after.IsFailure) return Outcome("Unreadable");

        var diff = CheckpointTextDiff.Compare(before.Value.Text, after.Value.Text);
        if (diff.IsEmpty) return Outcome("Unchanged", diff, 100);

        var total = diff.Unchanged + diff.Added.Count + diff.Changed.Count;
        var unchangedPercent = total == 0 ? 0 : (int)Math.Round(100.0 * diff.Unchanged / total);

        string Quote(string text) => CheckpointSummaryText.Clip(text, CheckpointSummaryLimits.MaxEvidenceCharacters);
        var items = new List<WorkspaceVersionChangeItemDto>();
        // Reworded sentences that now state other numbers come first: they are what a reviewer should check.
        items.AddRange(diff.Changed.OrderByDescending(item => item.FiguresChanged).ThenBy(item => item.After.Index)
            .Select(item => new WorkspaceVersionChangeItemDto
            {
                Kind = "Changed", Text = Quote(item.After.Text), Before = Quote(item.Before.Text), FiguresChanged = item.FiguresChanged,
                Page = after.Value.Pages?.Locate(item.After.Text)
            }));
        items.AddRange(diff.Added.OrderByDescending(item => item.Text.Length)
            .Select(item => new WorkspaceVersionChangeItemDto { Kind = "Added", Text = Quote(item.Text), Page = after.Value.Pages?.Locate(item.Text) }));
        items.AddRange(diff.Removed.OrderByDescending(item => item.Text.Length)
            .Select(item => new WorkspaceVersionChangeItemDto { Kind = "Removed", Text = Quote(item.Text), Page = before.Value.Pages?.Locate(item.Text) }));

        // Keep a balanced handful: the changed ones first, then the longest added and removed sentences.
        var shown = items.Where(item => item.Kind == "Changed").Take(MaximumItemsPerFile)
            .Concat(items.Where(item => item.Kind == "Added").Take(MaximumItemsPerFile))
            .Concat(items.Where(item => item.Kind == "Removed").Take(MaximumItemsPerFile)).ToArray();
        return Outcome("Compared", diff, unchangedPercent, shown);
    }

    private static bool IsReviewer(string role) =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase);
}
