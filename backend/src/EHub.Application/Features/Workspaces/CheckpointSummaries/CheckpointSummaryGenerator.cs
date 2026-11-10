using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public interface ICheckpointSummaryGenerator
{
    /// <returns>True when a file was handled (or lost to another worker), so the caller should poll again immediately.</returns>
    Task<bool> ProcessNextAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Summarises one queued file: reads its text, builds the quick view without the model, condenses the text to the
/// prompt budget and makes one short model call. Reviewers' requests run before files queued automatically after upload;
/// within a request the newest file version goes first. Rows are claimed with a lease so overlapping runs never process
/// the same file twice; transient failures are retried with a back-off; a reviewer can cancel, which aborts the model call.
/// </summary>
public sealed class CheckpointSummaryGenerator(
    IApplicationDbContext context,
    CheckpointFileText fileText,
    ICheckpointSummarizer summarizer,
    IDateTimeProvider dateTimeProvider,
    CheckpointSummarySettings settings,
    CheckpointSummaryTopics topics,
    ICheckpointSummaryCancellation cancellations,
    ILogger<CheckpointSummaryGenerator> logger,
    CheckpointSummaryCircuitBreaker? breaker = null) : ICheckpointSummaryGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Checklist items with related content before the summary follows the checklist outline.
    private const int MinimumCoveredItems = 2;

    private sealed record Generated(string ContentJson, string PreviewJson, bool InputTruncated);

    public async Task<bool> ProcessNextAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var candidate = await context.SubmissionFileSummaries.AsNoTracking()
            .Where(item => (item.Status == CheckpointSummaryStatus.Pending && (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= nowUtc)) ||
                (item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc <= nowUtc))
            .OrderBy(item => item.IsAutomatic).ThenBy(item => item.NextAttemptAtUtc).ThenByDescending(item => item.FileVersionNumber)
            .Select(item => new { item.Id, item.SubmissionFileId, item.CheckpointId, item.AttemptCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return false;
        // The model keeps failing: leave the file where it is (and its attempts untouched) until the cool-down is over.
        if (breaker is not null && !breaker.AllowRequest(nowUtc)) return false;

        // The lease doubles as the ownership token checked when saving. PostgreSQL keeps microseconds, so round to them
        // or the saved value would never equal the one held here.
        var lease = (nowUtc + CheckpointSummaryLimits.ProcessingLease).Ticks;
        var leaseUntil = new DateTime(lease - lease % 10, DateTimeKind.Utc);
        var claimed = await context.SubmissionFileSummaries
            .Where(item => item.Id == candidate.Id &&
                ((item.Status == CheckpointSummaryStatus.Pending && (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= nowUtc)) ||
                 (item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc <= nowUtc)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, CheckpointSummaryStatus.Processing)
                .SetProperty(item => item.NextAttemptAtUtc, leaseUntil), cancellationToken);
        if (claimed == 0) return true;

        using var registration = cancellations.Register(candidate.Id, out var cancelledByReviewer);
        // A reviewer may have cancelled between the claim and the registration; then there is nothing to abort.
        if (!await context.SubmissionFileSummaries.AnyAsync(item => item.Id == candidate.Id &&
                item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc == leaseUntil, cancellationToken))
            return true;

        Result<Generated> result;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancelledByReviewer))
        {
            timeout.CancelAfter(settings.JobTimeout);
            try
            {
                result = await GenerateAsync(candidate.Id, candidate.SubmissionFileId, candidate.CheckpointId, timeout.Token);
            }
            catch (OperationCanceledException) when (cancelledByReviewer.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // The reviewer's request already marked the row Cancelled; nothing to save.
                logger.LogInformation("File summary {SummaryId} was cancelled by a reviewer.", candidate.Id);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryModelTimeout, "The summary took too long.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Only the type is logged: messages from parsers or the model client can contain document text.
                logger.LogError("File summary {SummaryId} crashed with {ExceptionType}.", candidate.Id, exception.GetType().Name);
                result = Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryUnavailable, "The summary failed unexpectedly.");
            }
        }

        if (result.IsSuccess)
        {
            breaker?.RecordSuccess();
            var finishedAt = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
            await context.SubmissionFileSummaries
                .Where(item => item.Id == candidate.Id && item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc == leaseUntil)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, CheckpointSummaryStatus.Ready)
                    .SetProperty(item => item.ContentJson, result.Value.ContentJson)
                    .SetProperty(item => item.PreviewJson, result.Value.PreviewJson)
                    .SetProperty(item => item.InputTruncated, result.Value.InputTruncated)
                    .SetProperty(item => item.Model, summarizer.ModelName)
                    .SetProperty(item => item.PromptVersion, CheckpointSummaryPrompt.Version)
                    .SetProperty(item => item.GeneratedAtUtc, finishedAt)
                    .SetProperty(item => item.AttemptCount, 0)
                    .SetProperty(item => item.NextAttemptAtUtc, (DateTime?)null)
                    .SetProperty(item => item.LastErrorCode, (string?)null), cancellationToken);
            logger.LogInformation("File summary {SummaryId} is ready.", candidate.Id);
            return true;
        }

        // Only a model that cannot be reached or does not answer counts against the circuit; an unreadable file or a bad answer says
        // nothing about whether the model is up, so those outcomes leave the circuit as it is.
        DateTime? pausedUntil = null;
        if (breaker is not null && result.Error.Code is ErrorCodes.WorkspaceSummaryModelUnreachable or ErrorCodes.WorkspaceSummaryModelTimeout)
        {
            breaker.RecordFailure(nowUtc);
            var snapshot = breaker.Snapshot(nowUtc);
            if (snapshot.State == CheckpointCircuitState.Open) pausedUntil = snapshot.OpenUntilUtc;
        }
        await RecordFailureAsync(candidate.Id, candidate.AttemptCount + 1, leaseUntil, result.Error, nowUtc, pausedUntil, cancellationToken);
        return true;
    }

    private async Task<Result<Generated>> GenerateAsync(Guid summaryId, Guid fileId, Guid checkpointId, CancellationToken cancellationToken)
    {
        var checkpoint = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item => item.Id == checkpointId, cancellationToken);
        if (checkpoint is null)
            return Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryNoContent, "The checkpoint no longer exists.");
        var read = await fileText.ReadAsync(fileId, cancellationToken);
        if (read.IsFailure) return Result.Failure<Generated>(read.Error);
        var checklist = await CheckpointSummarySource.ChecklistAsync(context, checkpoint, cancellationToken);
        var text = read.Value.Text;

        // The quick view needs no model. Saving it first lets the screen show it while the model is still writing.
        var preview = CheckpointSummaryEvidence.Preview(text, checklist, topics, read.Value.LittleText, read.Value.IgnoredInstructions, read.Value.Pages);
        var previewJson = JsonSerializer.Serialize(preview, JsonOptions);
        await context.SubmissionFileSummaries
            .Where(item => item.Id == summaryId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PreviewJson, previewJson), cancellationToken);

        // Long files are condensed to their most informative sentences without a model, so one short call reads them.
        var condensed = text.Length > settings.FilePromptCharacters;
        var prompt = condensed
            ? CheckpointSummaryText.Select(text, CheckpointSummaryEvidence.ChecklistTerms(checklist), settings.FilePromptCharacters)
            : text;
        // The checklist outline is used only for a file the quick view found covering it (a pitch deck for a pitch
        // rubric); a progress review or a technical report is summarised on its own terms.
        var followChecklist = preview.Requirements.Count(item =>
            item.Finding == CheckpointSummaryEvidence.Related && item.Assessment == CheckpointCriterionProfile.Document) >= MinimumCoveredItems;
        // The outline names only the criteria the quick view found in the file: a small model shown "Market Research Quality" for
        // a business model canvas wrote a research method for it.
        var outline = followChecklist ? CheckpointSummaryEvidence.Covered(checklist, preview.Requirements) : checklist;
        var answer = await summarizer.CompleteJsonAsync(CheckpointSummaryPrompt.System,
            CheckpointSummaryPrompt.User(checkpoint.CheckpointNumber, checkpoint.Name, outline, read.Value.File.OriginalName, prompt,
                condensed, read.Value.LittleText, followChecklist, preview.StatusItems.Count > 0),
            CheckpointSummaryPrompt.Schema, cancellationToken);
        if (answer.IsFailure) return Result.Failure<Generated>(answer.Error);

        // Checked against the whole file, not only the condensed prompt: a detail from the file is never wrongly dropped.
        var content = CheckpointSummaryOutput.TryBuild(answer.Value, preview, new CheckpointSummaryGrounding(text, read.Value.LittleText));
        if (content is null)
            return Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryModelBadAnswer, "The model returned an answer that could not be used.");
        return Result.Success(new Generated(JsonSerializer.Serialize(content, JsonOptions), previewJson, read.Value.Truncated || condensed));
    }

    private async Task RecordFailureAsync(
        Guid summaryId, int attempts, DateTime leaseUntil, Error error, DateTime nowUtc, DateTime? pausedUntil, CancellationToken cancellationToken)
    {
        if (pausedUntil is { } resumeAt)
        {
            // The circuit is open: this was the model's fault, not the file's, so the file keeps the attempts it had and waits.
            await context.SubmissionFileSummaries
                .Where(item => item.Id == summaryId && item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc == leaseUntil)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, CheckpointSummaryStatus.Pending)
                    .SetProperty(item => item.NextAttemptAtUtc, resumeAt)
                    .SetProperty(item => item.LastErrorCode, error.Code), cancellationToken);
            logger.LogWarning("File summary {SummaryId} waits for the model (circuit open until {ResumeAt:O}): {Code}.", summaryId, resumeAt, error.Code);
            return;
        }

        // Missing or unreadable content will not improve with retries; an unreachable model or storage might, so
        // those are retried for much longer (a model container restart takes minutes).
        var permanent = error.Code is ErrorCodes.WorkspaceSummaryNoContent or ErrorCodes.WorkspaceSummaryUnreadable;
        var temporary = error.Code is ErrorCodes.WorkspaceSummaryModelUnreachable or ErrorCodes.WorkspaceSummaryModelTimeout
            or ErrorCodes.WorkspaceSummaryStorageUnavailable;
        var maxAttempts = temporary ? CheckpointSummaryLimits.MaxInfrastructureAttempts : CheckpointSummaryLimits.MaxAttempts;
        var failed = permanent || attempts >= maxAttempts;
        var delays = temporary ? CheckpointSummaryLimits.InfrastructureRetryDelays : CheckpointSummaryLimits.RetryDelays;
        var nextAttempt = nowUtc + delays[Math.Min(attempts - 1, delays.Length - 1)];

        await context.SubmissionFileSummaries
            .Where(item => item.Id == summaryId && item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc == leaseUntil)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, failed ? CheckpointSummaryStatus.Failed : CheckpointSummaryStatus.Pending)
                .SetProperty(item => item.AttemptCount, attempts)
                .SetProperty(item => item.NextAttemptAtUtc, failed ? (DateTime?)null : nextAttempt)
                .SetProperty(item => item.LastErrorCode, error.Code), cancellationToken);

        logger.LogWarning("File summary {SummaryId} failed (attempt {Attempt}, {Outcome}): {Code}.",
            summaryId, attempts, failed ? "giving up" : "will retry", error.Code);
    }
}
