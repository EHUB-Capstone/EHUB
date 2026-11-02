using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Reads, queues and cancels AI summaries of checkpoint files. Only admins, the class's lecturers and the team's active
/// mentors may use them; students never see a summary. Files are summarised one at a time by
/// <see cref="CheckpointSummaryGenerator"/>, and a file version is summarised at most once.
/// </summary>
public sealed class CheckpointSummaryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    CheckpointSummarySettings settings,
    ICheckpointSummaryQueueSignal queueSignal,
    ICheckpointSummaryCancellation cancellations,
    IValidator<RequestCheckpointSummaryRequest> validator) : ICheckpointSummaryHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<WorkspaceCheckpointSummaryResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointSummaryResponse>(access.Error);
        return Result.Success(await BuildResponseAsync(teamId, checkpointNumber, access.Value, cancellationToken));
    }

    public async Task<Result<WorkspaceCheckpointSummaryResponse>> RequestAsync(
        Guid teamId, int checkpointNumber, RequestCheckpointSummaryRequest request, Guid userId, string role,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return Result.Failure<WorkspaceCheckpointSummaryResponse>(ErrorCodes.WorkspaceSummaryDisabled,
                "AI summaries are not enabled on this server.");
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<WorkspaceCheckpointSummaryResponse>(ErrorCodes.WorkspaceValidationError, validation.Errors[0].ErrorMessage);
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointSummaryResponse>(access.Error);
        var checkpoint = access.Value;

        var source = await CheckpointSummarySource.LoadAsync(context, teamId, checkpoint, cancellationToken);
        var chosen = source.Files.Where(file => request.FileIds.Contains(file.Id)).ToArray();
        // Only files on this team's checkpoint can be summarised; an id from elsewhere is refused, not ignored.
        if (chosen.Length != request.FileIds.Count)
            return Result.Failure<WorkspaceCheckpointSummaryResponse>(ErrorCodes.WorkspaceNotFound,
                "One or more files are not part of this checkpoint any more. Refresh and try again.");

        var chosenIds = chosen.Select(file => file.Id).ToArray();
        var existing = await context.SubmissionFileSummaries
            .Where(item => chosenIds.Contains(item.SubmissionFileId))
            .ToDictionaryAsync(item => item.SubmissionFileId, cancellationToken);
        // A current summary of an unchanged file, or one already queued, never adds work.
        var toQueue = chosen.Where(file => !existing.TryGetValue(file.Id, out var summary) || NeedsWork(summary)).ToArray();
        if (toQueue.Length > 0)
        {
            var queued = await context.SubmissionFileSummaries.CountAsync(item =>
                item.Status == CheckpointSummaryStatus.Pending || item.Status == CheckpointSummaryStatus.Processing, cancellationToken);
            if (queued + toQueue.Length > CheckpointSummaryLimits.MaxQueuedSummaries)
                return Result.Failure<WorkspaceCheckpointSummaryResponse>(ErrorCodes.WorkspaceSummaryQueueFull,
                    "Many summaries are waiting. Please try again in a few minutes.");

            var now = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
            foreach (var file in toQueue)
            {
                if (!existing.TryGetValue(file.Id, out var summary))
                {
                    summary = new SubmissionFileSummary
                    {
                        SubmissionFileId = file.Id, TeamId = teamId, CheckpointId = checkpoint.Id,
                        FileVersionNumber = file.VersionNumber, CreatedAt = now, CreatedBy = userId
                    };
                    context.SubmissionFileSummaries.Add(summary);
                }
                else
                {
                    summary.UpdatedAt = now;
                    summary.UpdatedBy = userId;
                }
                // The same due time for the whole request; the worker then takes the newest version first.
                summary.Status = CheckpointSummaryStatus.Pending;
                summary.RequestedById = userId;
                summary.RequestedAtUtc = now;
                summary.AttemptCount = 0;
                summary.NextAttemptAtUtc = now;
                summary.LastErrorCode = null;
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Another reviewer queued the same file at the same moment: their request stands.
                context.ClearChanges();
            }
            // Start right away instead of waiting for the worker's next poll; the queued rows are committed.
            queueSignal.Signal();
        }

        return Result.Success(await BuildResponseAsync(teamId, checkpointNumber, checkpoint, cancellationToken));
    }

    public async Task<Result<WorkspaceCheckpointSummaryResponse>> CancelAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointSummaryResponse>(access.Error);
        var checkpointId = access.Value.Id;

        var running = context.SubmissionFileSummaries.Where(item => item.TeamId == teamId && item.CheckpointId == checkpointId &&
            (item.Status == CheckpointSummaryStatus.Pending || item.Status == CheckpointSummaryStatus.Processing));
        var runningIds = await running.Where(item => item.Status == CheckpointSummaryStatus.Processing)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var now = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
        // A conditional update, so a file the worker has just finished keeps its summary.
        await running.ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.Status, CheckpointSummaryStatus.Cancelled)
            .SetProperty(item => item.NextAttemptAtUtc, (DateTime?)null)
            .SetProperty(item => item.LastErrorCode, (string?)null)
            .SetProperty(item => item.UpdatedAt, now)
            .SetProperty(item => item.UpdatedBy, userId), cancellationToken);
        // Abort the model call if this instance's worker is running it.
        foreach (var id in runningIds) cancellations.Cancel(id);

        return Result.Success(await BuildResponseAsync(teamId, checkpointNumber, access.Value, cancellationToken));
    }

    private async Task<WorkspaceCheckpointSummaryResponse> BuildResponseAsync(
        Guid teamId, int checkpointNumber, Checkpoint checkpoint, CancellationToken cancellationToken)
    {
        var source = await CheckpointSummarySource.LoadAsync(context, teamId, checkpoint, cancellationToken);
        var fileIds = source.Files.Select(file => file.Id).ToArray();
        var summaries = await context.SubmissionFileSummaries.AsNoTracking()
            .Where(item => fileIds.Contains(item.SubmissionFileId))
            .ToDictionaryAsync(item => item.SubmissionFileId, cancellationToken);
        // The shared queue is small (bounded by MaxQueuedSummaries), so its order is read once.
        var queue = summaries.Values.Any(item => item.Status == CheckpointSummaryStatus.Pending)
            ? await context.SubmissionFileSummaries.AsNoTracking()
                .Where(item => item.Status == CheckpointSummaryStatus.Pending || item.Status == CheckpointSummaryStatus.Processing)
                .OrderByDescending(item => item.Status == CheckpointSummaryStatus.Processing)
                .ThenBy(item => item.NextAttemptAtUtc).ThenByDescending(item => item.FileVersionNumber)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken)
            : [];

        var files = source.Files.Select(file =>
        {
            summaries.TryGetValue(file.Id, out var summary);
            var content = summary?.Status == CheckpointSummaryStatus.Ready ? ReadContent(summary.ContentJson) : null;
            return new WorkspaceFileSummary
            {
                FileId = file.Id,
                Name = file.OriginalName,
                VersionNumber = file.VersionNumber,
                UploadedAt = file.UploadedAt,
                Status = summary is null ? "None" : summary.Status.ToString(),
                QueueAhead = summary?.Status == CheckpointSummaryStatus.Pending ? Math.Max(0, queue.IndexOf(summary.Id)) : null,
                RequestedAt = summary?.RequestedAtUtc,
                GeneratedAt = summary?.GeneratedAtUtc,
                ErrorCode = summary?.Status == CheckpointSummaryStatus.Failed ? summary.LastErrorCode : null,
                InputTruncated = content is not null && summary!.InputTruncated,
                Content = content
            };
        }).ToArray();

        return new WorkspaceCheckpointSummaryResponse
        {
            CheckpointNumber = checkpointNumber,
            Enabled = settings.Enabled,
            FilePromptCharacters = settings.FilePromptCharacters,
            Files = files,
            Requirements = CheckpointSummaryOutput.Combine(source.Requirements,
                files.Where(file => file.Content is not null).Select(file => (file.Name, file.Content!)).ToArray()),
            Links = source.Links.Select(link => new WorkspaceCheckpointSummaryLink
            {
                Name = link.Name, Domain = CheckpointSummaryOutput.Domain(link.Url)
            }).ToArray()
        };
    }

    /// <summary>Pending or running work is left alone; a ready summary is redone only if it came from an older prompt.</summary>
    private static bool NeedsWork(SubmissionFileSummary summary) => summary.Status switch
    {
        CheckpointSummaryStatus.Pending or CheckpointSummaryStatus.Processing => false,
        CheckpointSummaryStatus.Ready => summary.PromptVersion != CheckpointSummaryPrompt.Version || ReadContent(summary.ContentJson) is null,
        _ => true
    };

    private static WorkspaceFileSummaryContent? ReadContent(string? json)
    {
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<WorkspaceFileSummaryContent>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private async Task<Result<Checkpoint>> ResolveAccessAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken)
    {
        // Summaries support grading conversations; students are refused before any lookup.
        if (CheckpointWorkspaceAccess.IsStudent(role) || !IsReviewer(role))
            return Result.Failure<Checkpoint>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to AI summaries for this team.");
        var access = await CheckpointWorkspaceAccess.ResolveAsync(context, teamId, checkpointNumber, userId, role, cancellationToken);
        return access.IsSuccess ? Result.Success(access.Value.Checkpoint) : Result.Failure<Checkpoint>(access.Error);
    }

    private static bool IsReviewer(string role) =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase);
}
