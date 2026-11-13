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

public interface ICheckpointSummaryFeedbackHandler
{
    Task<Result<WorkspaceSummaryFeedback>> SetAsync(
        Guid teamId, int checkpointNumber, Guid fileId, SetCheckpointSummaryFeedbackRequest request, Guid userId, string role,
        CancellationToken cancellationToken = default);

    Task<Result> ClearAsync(
        Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, Guid? generationId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lets a reviewer say whether an AI summary was useful. This is how quality is measured on real submissions: nothing else tells us
/// whether a change of prompt or rules helped. Only staff with access to the team may rate, only a finished summary of that team's
/// checkpoint can be rated, and each reviewer sees and changes only their own verdict.
/// </summary>
public sealed class CheckpointSummaryFeedbackHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    CheckpointSummarySettings settings) : ICheckpointSummaryFeedbackHandler
{
    public static readonly IReadOnlySet<string> Reasons =
        new HashSet<string>(StringComparer.Ordinal) { "Inaccurate", "MissingKeyPoint", "WrongEvidence", "Other" };

    public async Task<Result<WorkspaceSummaryFeedback>> SetAsync(
        Guid teamId, int checkpointNumber, Guid fileId, SetCheckpointSummaryFeedbackRequest request, Guid userId, string role,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return Result.Failure<WorkspaceSummaryFeedback>(ErrorCodes.WorkspaceSummaryDisabled, "AI summaries are not enabled on this server.");
        if (!Enum.TryParse<CheckpointSummaryFeedbackRating>(request.Rating, ignoreCase: false, out var rating) || !Enum.IsDefined(rating))
            return Result.Failure<WorkspaceSummaryFeedback>(ErrorCodes.WorkspaceValidationError, "Rating must be Helpful or NotHelpful.");
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is not null && (rating != CheckpointSummaryFeedbackRating.NotHelpful || !Reasons.Contains(reason)))
            return Result.Failure<WorkspaceSummaryFeedback>(ErrorCodes.WorkspaceValidationError, "That reason cannot be used.");

        var summary = await FindRatableSummaryAsync(teamId, checkpointNumber, fileId, userId, role, cancellationToken);
        if (summary.IsFailure) return Result.Failure<WorkspaceSummaryFeedback>(summary.Error);
        var generationId = CheckpointSummarySnapshot.GenerationId(summary.Value.ContentJson);
        if (request.GenerationId is { } expected && expected != generationId)
            return Result.Failure<WorkspaceSummaryFeedback>(ErrorCodes.WorkspaceConcurrencyConflict,
                "This summary has been regenerated. Reload it before giving feedback.");

        var now = CheckpointWorkspaceAccess.EnsureUtc(dateTimeProvider.UtcNow);
        // A second tab can create the same verdict between the read and the save; the loser simply updates the winner's row.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var feedback = await context.CheckpointSummaryFeedbacks
                .FirstOrDefaultAsync(item => item.SubmissionFileSummaryId == summary.Value.Id && item.UserId == userId, cancellationToken);
            if (feedback is null)
            {
                feedback = new CheckpointSummaryFeedback
                {
                    SubmissionFileSummaryId = summary.Value.Id, UserId = userId, CreatedAt = now, CreatedBy = userId
                };
                context.CheckpointSummaryFeedbacks.Add(feedback);
            }
            else
            {
                feedback.UpdatedAt = now;
                feedback.UpdatedBy = userId;
            }
            feedback.Rating = rating;
            feedback.Reason = reason;
            feedback.PromptVersion = summary.Value.PromptVersion;
            feedback.Model = summary.Value.Model;
            feedback.SummaryGenerationId = generationId;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                context.ClearChanges();
            }
        }
        return Result.Success(new WorkspaceSummaryFeedback { Rating = rating.ToString(), Reason = reason });
    }

    public async Task<Result> ClearAsync(
        Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, Guid? generationId = null, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled) return Result.Failure(ErrorCodes.WorkspaceSummaryDisabled, "AI summaries are not enabled on this server.");
        var summary = await FindRatableSummaryAsync(teamId, checkpointNumber, fileId, userId, role, cancellationToken);
        if (summary.IsFailure) return Result.Failure(summary.Error);
        if (generationId is { } expected && expected != CheckpointSummarySnapshot.GenerationId(summary.Value.ContentJson))
            return Result.Failure(ErrorCodes.WorkspaceConcurrencyConflict,
                "This summary has been regenerated. Reload it before clearing feedback.");
        // A real delete, not a soft one: the unique index must not keep a cleared verdict from being given again.
        await context.CheckpointSummaryFeedbacks
            .Where(item => item.SubmissionFileSummaryId == summary.Value.Id && item.UserId == userId &&
                (generationId == null || item.SummaryGenerationId == generationId))
            .ExecuteDeleteAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<SubmissionFileSummary>> FindRatableSummaryAsync(
        Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role, CancellationToken cancellationToken)
    {
        // Students are refused before any lookup, like every other summary endpoint.
        if (!IsReviewer(role))
            return Result.Failure<SubmissionFileSummary>(ErrorCodes.WorkspaceAccessDenied, "You do not have access to AI summaries for this team.");
        var access = await CheckpointWorkspaceAccess.ResolveAsync(context, teamId, checkpointNumber, userId, role, cancellationToken);
        if (access.IsFailure) return Result.Failure<SubmissionFileSummary>(access.Error);

        var checkpointId = access.Value.Checkpoint.Id;
        // The file must belong to this team's checkpoint: a file id from another team is answered as "not found", not as "forbidden".
        var summary = await context.SubmissionFileSummaries.AsNoTracking().FirstOrDefaultAsync(item =>
            item.SubmissionFileId == fileId && item.TeamId == teamId && item.CheckpointId == checkpointId &&
            item.Status == CheckpointSummaryStatus.Ready, cancellationToken);
        return summary is null
            ? Result.Failure<SubmissionFileSummary>(ErrorCodes.WorkspaceNotFound, "Only a finished summary of this checkpoint can be rated.")
            : Result.Success(summary);
    }

    private static bool IsReviewer(string role) =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase);
}
