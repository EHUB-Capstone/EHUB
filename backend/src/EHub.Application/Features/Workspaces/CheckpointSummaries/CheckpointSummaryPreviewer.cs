using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Workspaces;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public interface ICheckpointSummaryPreviewer
{
    /// <returns>True when a file was handled, so the caller should poll again immediately.</returns>
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Works out the quick view (key sentences and checklist evidence) of queued files without the model. It takes about a
/// second per file, so reviewers see it soon after upload while the model works through the queue one file at a time.
/// </summary>
public sealed class CheckpointSummaryPreviewer(
    IApplicationDbContext context,
    CheckpointFileText fileText,
    CheckpointSummaryTopics topics,
    ILogger<CheckpointSummaryPreviewer> logger) : ICheckpointSummaryPreviewer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Stored when the file cannot be read, so it is not retried; the screen treats it as "not read".</summary>
    private static readonly string Empty = JsonSerializer.Serialize(new WorkspaceFilePreview(), JsonOptions);

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        // Running files get their quick view from the generator itself.
        var candidate = await context.SubmissionFileSummaries.AsNoTracking()
            .Where(item => item.Status == CheckpointSummaryStatus.Pending && item.PreviewJson == null)
            .OrderBy(item => item.IsAutomatic).ThenBy(item => item.RequestedAtUtc).ThenByDescending(item => item.FileVersionNumber)
            .Select(item => new { item.Id, item.SubmissionFileId, item.CheckpointId })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return false;

        var previewJson = Empty;
        var checkpoint = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item => item.Id == candidate.CheckpointId, cancellationToken);
        if (checkpoint is not null)
        {
            var read = await fileText.ReadAsync(candidate.SubmissionFileId, cancellationToken);
            if (read.IsSuccess)
            {
                var checklist = await CheckpointSummarySource.ChecklistAsync(context, checkpoint, cancellationToken);
                previewJson = JsonSerializer.Serialize(CheckpointSummaryEvidence.Preview(read.Value.Text, checklist, topics, read.Value.LittleText, read.Value.IgnoredInstructions, read.Value.Pages), JsonOptions);
            }
            else if (read.Error.Code == ErrorCodes.WorkspaceSummaryStorageUnavailable)
            {
                // Storage is down: the generator will read the file when it gets to it and save the real quick view.
                logger.LogWarning("Quick view of file summary {SummaryId} skipped: storage unavailable.", candidate.Id);
            }
        }

        // Conditional, so a quick view the generator saved in the meantime is kept.
        await context.SubmissionFileSummaries
            .Where(item => item.Id == candidate.Id && item.PreviewJson == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PreviewJson, previewJson), cancellationToken);
        return true;
    }
}
