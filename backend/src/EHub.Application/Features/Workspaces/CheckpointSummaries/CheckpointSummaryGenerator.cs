using System.Text;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
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
    /// <returns>True when a summary was handled (or lost to another worker), so the caller should poll again immediately.</returns>
    Task<bool> ProcessNextAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Produces one queued checkpoint summary: reads the team's current files (newest first, up to the input limit),
/// summarises long files in chunks, asks for the final structured summary and validates it. Summaries are claimed with
/// a lease so overlapping runs never process the same one twice; transient failures are retried with a back-off.
/// </summary>
public sealed class CheckpointSummaryGenerator(
    IApplicationDbContext context,
    ISubmissionObjectStorage objectStorage,
    ISubmissionFileStorageService legacyStorage,
    ISubmissionTextExtractor extractor,
    ICheckpointSummarizer summarizer,
    IDateTimeProvider dateTimeProvider,
    CheckpointSummarySettings settings,
    ILogger<CheckpointSummaryGenerator> logger) : ICheckpointSummaryGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record Generated(string ContentJson, string SourceHash, bool InputTruncated);

    public async Task<bool> ProcessNextAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var candidate = await context.CheckpointSummaries.AsNoTracking()
            .Where(item => (item.Status == CheckpointSummaryStatus.Pending && (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= nowUtc)) ||
                (item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc <= nowUtc))
            .OrderBy(item => item.NextAttemptAtUtc)
            .Select(item => new { item.Id, item.TeamId, item.CheckpointId, item.AttemptCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return false;

        // The lease doubles as the ownership token checked when saving. PostgreSQL keeps microseconds, so round to them
        // or the saved value would never equal the one held here.
        var lease = (nowUtc + CheckpointSummaryLimits.ProcessingLease).Ticks;
        var leaseUntil = new DateTime(lease - lease % 10, DateTimeKind.Utc);
        var claimed = await context.CheckpointSummaries
            .Where(item => item.Id == candidate.Id &&
                ((item.Status == CheckpointSummaryStatus.Pending && (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= nowUtc)) ||
                 (item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc <= nowUtc)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, CheckpointSummaryStatus.Processing)
                .SetProperty(item => item.NextAttemptAtUtc, leaseUntil), cancellationToken);
        if (claimed == 0) return true;

        Result<Generated> result;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(settings.JobTimeout);
            try
            {
                result = await GenerateAsync(candidate.TeamId, candidate.CheckpointId, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryUnavailable, "The summary took too long.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Only the type is logged: messages from parsers or the model client can contain document text.
                logger.LogError("Checkpoint summary {SummaryId} crashed with {ExceptionType}.", candidate.Id, exception.GetType().Name);
                result = Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryUnavailable, "The summary failed unexpectedly.");
            }
        }

        if (result.IsSuccess)
        {
            var finishedAt = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
            await context.CheckpointSummaries
                .Where(item => item.Id == candidate.Id && item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc == leaseUntil)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, CheckpointSummaryStatus.Ready)
                    .SetProperty(item => item.ContentJson, result.Value.ContentJson)
                    .SetProperty(item => item.SourceHash, result.Value.SourceHash)
                    .SetProperty(item => item.InputTruncated, result.Value.InputTruncated)
                    .SetProperty(item => item.Model, summarizer.ModelName)
                    .SetProperty(item => item.PromptVersion, CheckpointSummaryPrompt.Version)
                    .SetProperty(item => item.GeneratedAtUtc, finishedAt)
                    .SetProperty(item => item.AttemptCount, 0)
                    .SetProperty(item => item.NextAttemptAtUtc, (DateTime?)null)
                    .SetProperty(item => item.LastErrorCode, (string?)null), cancellationToken);
            logger.LogInformation("Checkpoint summary {SummaryId} is ready.", candidate.Id);
            return true;
        }

        await RecordFailureAsync(candidate.Id, candidate.AttemptCount + 1, leaseUntil, result.Error, nowUtc, cancellationToken);
        return true;
    }

    private async Task<Result<Generated>> GenerateAsync(Guid teamId, Guid checkpointId, CancellationToken cancellationToken)
    {
        var checkpoint = await context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(item => item.Id == checkpointId, cancellationToken);
        if (checkpoint is null) return NoContent();
        var source = await CheckpointSummarySource.LoadAsync(context, teamId, checkpoint, cancellationToken);
        if (source.IsEmpty) return NoContent();

        var budget = settings.MaxInputCharacters;
        var truncated = false;
        var sourceText = new StringBuilder();
        var answers = new StringBuilder();
        foreach (var requirement in source.Requirements.Where(item => !string.IsNullOrWhiteSpace(item.TeamAnswer)))
        {
            var answer = CheckpointSummaryText.Redact(requirement.TeamAnswer!.Trim());
            if (answer.Length > budget) { answer = answer[..Math.Max(0, budget)]; truncated = true; }
            if (answer.Length == 0) break;
            answers.Append("Yêu cầu ").Append(requirement.Index + 1).Append(": ").Append(answer).Append('\n');
            budget -= answer.Length;
        }
        sourceText.Append(answers);

        // Newest files first, so the limit drops older versions rather than the latest work.
        var files = new List<SummaryFileText>();
        foreach (var file in source.Files)
        {
            if (budget <= 0)
            {
                truncated = true;
                files.Add(new SummaryFileText(file, null, CheckpointSummaryOutput.FileNotIncluded));
                continue;
            }
            // One character over the budget tells a document that fills it exactly apart from a longer one.
            var text = await ReadTextAsync(file, budget + 1, cancellationToken);
            if (text.IsFailure)
            {
                if (text.Error.Code != ErrorCodes.WorkspaceSummaryUnreadable) return Result.Failure<Generated>(text.Error);
                files.Add(new SummaryFileText(file, null, CheckpointSummaryOutput.FileUnreadable));
                continue;
            }
            var value = CheckpointSummaryText.Redact(text.Value).Trim();
            if (value.Length > budget) { value = value[..budget]; truncated = true; }
            if (value.Length == 0)
            {
                files.Add(new SummaryFileText(file, null, CheckpointSummaryOutput.FileUnreadable));
                continue;
            }
            budget -= value.Length;
            sourceText.Append(value).Append('\n');
            files.Add(new SummaryFileText(file, value, CheckpointSummaryOutput.FileSummarized));
        }
        if (sourceText.Length == 0)
            return Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryUnreadable,
                "No text could be read from the submitted files. Scanned PDFs and images are not supported.");

        var documents = new List<string>();
        if (answers.Length > 0) documents.Add(CheckpointSummaryPrompt.Document("Câu trả lời của nhóm theo từng yêu cầu", answers.ToString(), notes: false));
        foreach (var file in files.Where(item => item.Text is not null))
        {
            if (file.Text!.Length <= CheckpointSummaryLimits.RawFileCharacters)
            {
                documents.Add(CheckpointSummaryPrompt.Document(file.File.OriginalName, file.Text, notes: false));
                continue;
            }
            var notes = new StringBuilder();
            var part = 0;
            foreach (var chunk in CheckpointSummaryText.Chunks(file.Text, CheckpointSummaryLimits.ChunkCharacters))
            {
                part++;
                var mapped = await summarizer.CompleteJsonAsync(CheckpointSummaryPrompt.System,
                    CheckpointSummaryPrompt.MapUser(file.File.OriginalName, part, chunk), CheckpointSummaryPrompt.MapSchema, cancellationToken);
                if (mapped.IsFailure) return Result.Failure<Generated>(mapped.Error);
                if (!CheckpointSummaryOutput.TryParseMap(mapped.Value, out var summary, out var quotes)) return InvalidOutput();
                notes.Append("[Phần ").Append(part).Append("] ").Append(summary).Append("\nTrích dẫn:\n");
                foreach (var quote in quotes) notes.Append("- ").Append(quote).Append('\n');
                notes.Append('\n');
            }
            documents.Add(CheckpointSummaryPrompt.Document(file.File.OriginalName, notes.ToString(), notes: true));
        }

        var links = source.Links.Select(link => (link.Name, CheckpointSummaryOutput.Domain(link.Url))).ToArray();
        var final = await summarizer.CompleteJsonAsync(CheckpointSummaryPrompt.System,
            CheckpointSummaryPrompt.FinalUser(checkpoint.CheckpointNumber, checkpoint.Name, source.Requirements, links, documents),
            CheckpointSummaryPrompt.FinalSchema(source.Requirements.Count), cancellationToken);
        if (final.IsFailure) return Result.Failure<Generated>(final.Error);
        var content = CheckpointSummaryOutput.TryBuild(final.Value, files, source.Links, source.Requirements, sourceText.ToString());
        if (content is null) return InvalidOutput();
        return Result.Success(new Generated(JsonSerializer.Serialize(content, JsonOptions), source.Hash, truncated));
    }

    private async Task<Result<string>> ReadTextAsync(SummarySourceFile file, int maxCharacters, CancellationToken cancellationToken)
    {
        if (file.FileSize > CheckpointSummaryLimits.MaxFileSizeBytes) return Unreadable();
        Result<byte[]> bytes;
        if (file.StorageProvider == SubmissionStorageProvider.R2)
        {
            if (string.IsNullOrWhiteSpace(file.StorageKey)) return Unreadable();
            bytes = await objectStorage.DownloadAsync(file.StorageKey, cancellationToken);
        }
        else
        {
            var download = await legacyStorage.DownloadAsync(file.FileUrl, cancellationToken);
            bytes = download.IsSuccess ? Result.Success(download.Value.Content) : Result.Failure<byte[]>(download.Error);
        }
        if (bytes.IsFailure)
        {
            // A missing object will not come back; storage outages are retried.
            return bytes.Error.Code == ErrorCodes.CommonNotFoundError
                ? Unreadable()
                : Result.Failure<string>(ErrorCodes.WorkspaceSummaryUnavailable, "Submitted files could not be downloaded.");
        }
        var extension = Path.GetExtension(file.OriginalName).ToLowerInvariant();
        return await extractor.ExtractAsync(bytes.Value, extension, maxCharacters, cancellationToken);
    }

    private async Task RecordFailureAsync(
        Guid summaryId, int attempts, DateTime leaseUntil, Error error, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Missing or unreadable content will not improve with retries; an unavailable model or storage might.
        var permanent = error.Code is ErrorCodes.WorkspaceSummaryNoContent or ErrorCodes.WorkspaceSummaryUnreadable;
        var failed = permanent || attempts >= CheckpointSummaryLimits.MaxAttempts;
        var delays = CheckpointSummaryLimits.RetryDelays;
        var nextAttempt = nowUtc + delays[Math.Min(attempts - 1, delays.Length - 1)];

        await context.CheckpointSummaries
            .Where(item => item.Id == summaryId && item.Status == CheckpointSummaryStatus.Processing && item.NextAttemptAtUtc == leaseUntil)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, failed ? CheckpointSummaryStatus.Failed : CheckpointSummaryStatus.Pending)
                .SetProperty(item => item.AttemptCount, attempts)
                .SetProperty(item => item.NextAttemptAtUtc, failed ? (DateTime?)null : nextAttempt)
                .SetProperty(item => item.LastErrorCode, error.Code), cancellationToken);

        logger.LogWarning("Checkpoint summary {SummaryId} failed (attempt {Attempt}, {Outcome}): {Code}.",
            summaryId, attempts, failed ? "giving up" : "will retry", error.Code);
    }

    private static Result<Generated> NoContent() => Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryNoContent,
        "The team has not submitted anything to summarise for this checkpoint.");

    private static Result<Generated> InvalidOutput() => Result.Failure<Generated>(ErrorCodes.WorkspaceSummaryUnavailable,
        "The model returned an answer that could not be used.");

    private static Result<string> Unreadable() => Result.Failure<string>(ErrorCodes.WorkspaceSummaryUnreadable,
        "The file could not be read for summarising.");
}
