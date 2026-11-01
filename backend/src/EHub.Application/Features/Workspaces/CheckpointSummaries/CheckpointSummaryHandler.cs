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
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <summary>
/// Reads and queues AI checkpoint summaries. Only admins, the class's lecturers and the team's active mentors may use
/// them; students never see a summary. Requests are queued and processed by <see cref="CheckpointSummaryGenerator"/>.
/// </summary>
public sealed class CheckpointSummaryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    CheckpointSummarySettings settings) : ICheckpointSummaryHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<WorkspaceCheckpointSummaryResponse>> GetAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointSummaryResponse>(access.Error);

        var summary = await context.CheckpointSummaries.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TeamId == teamId && item.CheckpointId == access.Value.Id, cancellationToken);
        var source = await CheckpointSummarySource.LoadAsync(context, teamId, access.Value, cancellationToken);
        return Result.Success(ToResponse(checkpointNumber, summary, source.Hash));
    }

    public async Task<Result<WorkspaceCheckpointSummaryResponse>> RequestAsync(
        Guid teamId, int checkpointNumber, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return Result.Failure<WorkspaceCheckpointSummaryResponse>(ErrorCodes.WorkspaceSummaryDisabled,
                "AI summaries are not enabled on this server.");
        var access = await ResolveAccessAsync(teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<WorkspaceCheckpointSummaryResponse>(access.Error);
        var checkpoint = access.Value;

        var source = await CheckpointSummarySource.LoadAsync(context, teamId, checkpoint, cancellationToken);
        if (source.IsEmpty)
            return Result.Failure<WorkspaceCheckpointSummaryResponse>(ErrorCodes.WorkspaceSummaryNoContent,
                "The team has not submitted any file, link or requirement answer for this checkpoint yet.");

        var summary = await context.CheckpointSummaries
            .FirstOrDefaultAsync(item => item.TeamId == teamId && item.CheckpointId == checkpoint.Id, cancellationToken);
        // Asking again while queued, or for unchanged content, never adds work.
        if (summary is not null && (summary.Status is CheckpointSummaryStatus.Pending or CheckpointSummaryStatus.Processing ||
            (summary.Status == CheckpointSummaryStatus.Ready && summary.SourceHash == source.Hash)))
            return Result.Success(ToResponse(checkpointNumber, summary, source.Hash));

        var queued = await context.CheckpointSummaries.CountAsync(item =>
            item.Status == CheckpointSummaryStatus.Pending || item.Status == CheckpointSummaryStatus.Processing, cancellationToken);
        if (queued >= CheckpointSummaryLimits.MaxQueuedSummaries)
            return Result.Failure<WorkspaceCheckpointSummaryResponse>(ErrorCodes.WorkspaceSummaryQueueFull,
                "Many summaries are waiting. Please try again in a few minutes.");

        var now = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
        if (summary is null)
        {
            summary = new CheckpointSummary
            {
                TeamId = teamId, CheckpointId = checkpoint.Id, SourceHash = source.Hash,
                CreatedAt = now, CreatedBy = userId
            };
            context.CheckpointSummaries.Add(summary);
        }
        else
        {
            summary.UpdatedAt = now;
            summary.UpdatedBy = userId;
        }
        // SourceHash keeps describing the previous content until the new summary is saved, so it still shows as stale.
        summary.Status = CheckpointSummaryStatus.Pending;
        summary.RequestedById = userId;
        summary.RequestedAtUtc = now;
        summary.AttemptCount = 0;
        summary.NextAttemptAtUtc = now;
        summary.LastErrorCode = null;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another reviewer queued the same team/checkpoint at the same moment: report their request.
            context.ClearChanges();
            var current = await context.CheckpointSummaries.AsNoTracking()
                .FirstOrDefaultAsync(item => item.TeamId == teamId && item.CheckpointId == checkpoint.Id, cancellationToken);
            if (current is null) throw;
            return Result.Success(ToResponse(checkpointNumber, current, source.Hash));
        }

        return Result.Success(ToResponse(checkpointNumber, summary, source.Hash));
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

    private WorkspaceCheckpointSummaryResponse ToResponse(int checkpointNumber, CheckpointSummary? summary, string currentHash)
    {
        var content = summary?.ContentJson is null
            ? null
            : JsonSerializer.Deserialize<WorkspaceCheckpointSummaryContent>(summary.ContentJson, JsonOptions);
        return new WorkspaceCheckpointSummaryResponse
        {
            CheckpointNumber = checkpointNumber,
            Status = summary?.Status.ToString() ?? (settings.Enabled ? "None" : "Disabled"),
            IsStale = content is not null && summary!.SourceHash != currentHash,
            InputTruncated = content is not null && summary!.InputTruncated,
            MaxInputCharacters = settings.MaxInputCharacters,
            RequestedAt = summary?.RequestedAtUtc,
            GeneratedAt = summary?.GeneratedAtUtc,
            Model = content is null ? null : summary!.Model,
            ErrorCode = summary?.Status == CheckpointSummaryStatus.Failed ? summary.LastErrorCode : null,
            Content = content
        };
    }

    private static bool IsReviewer(string role) =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase);
}
