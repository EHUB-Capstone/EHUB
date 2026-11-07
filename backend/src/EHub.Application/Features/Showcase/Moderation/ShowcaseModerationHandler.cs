using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Moderation;

public sealed class ShowcaseModerationHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IShowcaseModerationHandler
{
    public const int MaximumNoteLength = 1000;
    public const int MaximumPageSize = 50;

    private const string TeamLink = "/showcase/manage";
    private const string ReviewerLink = "/showcase/moderation";

    public async Task<Result<ShowcaseDto>> SubmitAsync(
        Guid showcaseId, SubmitShowcaseRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsStudent(role)) return Denied<ShowcaseDto>("Only students can submit a showcase page.");
        var showcase = await LoadOwnedAsync(showcaseId, userId, cancellationToken);
        if (showcase is null) return NotFound<ShowcaseDto>();

        if (showcase.Status is not (ShowcaseStatus.Draft or ShowcaseStatus.Hidden))
            return InvalidState<ShowcaseDto>($"A page in status {showcase.Status} cannot be submitted for review.");
        if (!request.AcknowledgedPublishWarning)
            return Invalid<ShowcaseDto>("Confirm that the content may be shown publicly before submitting.");

        // The page text comes from the Project Profile, so completeness is judged there.
        var project = await ShowcaseAccess.LatestProjectAsync(context, showcase.ProjectLineageId, cancellationToken);
        if (project is null) return NotFound<ShowcaseDto>();
        if (string.IsNullOrWhiteSpace(project.Description) || string.IsNullOrWhiteSpace(project.Problem) || string.IsNullOrWhiteSpace(project.Solution))
            return Invalid<ShowcaseDto>("Complete the Description, Problem and Solution in your Project Profile before submitting.");

        var now = dateTimeProvider.UtcNow;
        Transition(showcase, ShowcaseStatus.PendingReview, "Submitted for review.", userId, now);
        showcase.ReviewNote = null;

        ShowcaseOutbox.EnqueueNotification(
            context, showcase.Id,
            project.LecturerUserIds,
            "Showcase page awaiting review",
            $"“{project.ProjectName}” from team {project.TeamName} was submitted for publication.",
            ReviewerLink, now,
            // Fall back to administrators when the class has no lecturer to review it.
            notifyAdministrators: project.LecturerUserIds.Count == 0);

        return await SaveAsync(showcase, canEdit: false, cancellationToken);
    }

    public async Task<Result<ShowcaseDto>> WithdrawAsync(
        Guid showcaseId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsStudent(role)) return Denied<ShowcaseDto>("Only students can withdraw a showcase page.");
        var showcase = await LoadOwnedAsync(showcaseId, userId, cancellationToken);
        if (showcase is null) return NotFound<ShowcaseDto>();

        if (showcase.Status is not (ShowcaseStatus.Published or ShowcaseStatus.PendingReview))
            return InvalidState<ShowcaseDto>($"A page in status {showcase.Status} is not visible or pending.");

        Transition(showcase, ShowcaseStatus.Hidden, "Withdrawn by the project team.", userId, dateTimeProvider.UtcNow);
        return await SaveAsync(showcase, canEdit: true, cancellationToken);
    }

    public async Task<Result<ShowcaseDto>> DecideAsync(
        Guid showcaseId, ShowcaseModerationAction action, string? note, Guid userId, string role,
        CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsModerator(role)) return Denied<ShowcaseDto>("Only lecturers and administrators can review showcase pages.");

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > MaximumNoteLength })
            return Invalid<ShowcaseDto>($"The note must be at most {MaximumNoteLength} characters.");

        // Pages outside the reviewer's classes are indistinguishable from missing ones.
        var showcase = await ShowcaseAccess.ModeratableShowcases(context, userId, role)
            .FirstOrDefaultAsync(item => item.Id == showcaseId, cancellationToken);
        if (showcase is null) return NotFound<ShowcaseDto>();

        var rule = Rules[action];
        if (showcase.Status != rule.From)
            return InvalidState<ShowcaseDto>($"Only a page in status {rule.From} can be {rule.Verb}; this one is {showcase.Status}.");
        if (rule.NoteRequired && trimmedNote is null)
            return Invalid<ShowcaseDto>("A note explaining the decision is required.");
        if (action == ShowcaseModerationAction.Unhide && showcase.PublishedAt is null)
            return InvalidState<ShowcaseDto>("This page was never published. Ask the team to submit it for review.");

        var now = dateTimeProvider.UtcNow;
        Transition(showcase, rule.To, trimmedNote ?? rule.DefaultNote, userId, now);
        showcase.ReviewNote = action is ShowcaseModerationAction.Approve or ShowcaseModerationAction.Unhide ? null : trimmedNote;
        if (action == ShowcaseModerationAction.Approve) showcase.PublishedAt ??= now;

        var project = await ShowcaseAccess.LatestProjectAsync(context, showcase.ProjectLineageId, cancellationToken);
        if (project is not null)
        {
            ShowcaseOutbox.EnqueueNotification(
                context, showcase.Id, project.MemberUserIds,
                rule.NotificationTitle,
                trimmedNote is null ? $"“{project.ProjectName}”: {rule.NotificationBody}" : $"“{project.ProjectName}”: {rule.NotificationBody} Note: {trimmedNote}",
                TeamLink, now);
        }

        return await SaveAsync(showcase, canEdit: false, cancellationToken);
    }

    public async Task<Result<ShowcaseModerationPageDto>> ListAsync(
        string? status, int page, int pageSize, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsModerator(role)) return Denied<ShowcaseModerationPageDto>("Only lecturers and administrators can review showcase pages.");

        var statusFilter = ShowcaseStatus.PendingReview;
        if (!string.IsNullOrWhiteSpace(status) && !Enum.TryParse(status, true, out statusFilter))
            return Invalid<ShowcaseModerationPageDto>("Status filter is invalid.");
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = ShowcaseAccess.ModeratableShowcases(context, userId, role).AsNoTracking()
            .Where(item => item.Status == statusFilter);
        var total = await query.CountAsync(cancellationToken);
        var showcases = await query
            .OrderBy(item => item.UpdatedAt ?? item.CreatedAt).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var lineageIds = showcases.Select(item => item.ProjectLineageId).ToList();
        var projects = await context.Projects.AsNoTracking()
            .Where(project => lineageIds.Contains(project.ProjectLineageId))
            .Select(project => new
            {
                project.ProjectLineageId,
                project.CreatedAt,
                project.Name,
                project.Description,
                project.Team.TeamName,
                project.Team.Class.ClassCode
            })
            .ToListAsync(cancellationToken);

        var items = showcases.Select(showcase =>
        {
            var project = projects.Where(item => item.ProjectLineageId == showcase.ProjectLineageId)
                .OrderByDescending(item => item.CreatedAt).FirstOrDefault();
            return new ShowcaseModerationItemDto
            {
                Id = showcase.Id,
                ProjectName = project?.Name ?? string.Empty,
                Description = project?.Description ?? string.Empty,
                LogoUrl = showcase.LogoUrl,
                Status = showcase.Status.ToString(),
                DevelopmentStage = showcase.DevelopmentStage.ToString(),
                TeamName = project?.TeamName ?? string.Empty,
                ClassCode = project?.ClassCode ?? string.Empty,
                ReviewNote = showcase.ReviewNote,
                PublishedAt = showcase.PublishedAt,
                IsFeatured = showcase.IsFeatured,
                FeaturedAt = showcase.FeaturedAt,
                SubmittedOrUpdatedAt = showcase.UpdatedAt ?? showcase.CreatedAt
            };
        }).ToArray();

        return Result.Success(new ShowcaseModerationPageDto { Items = items, Total = total, Page = page, PageSize = pageSize });
    }

    private sealed record Rule(
        ShowcaseStatus From, ShowcaseStatus To, string Verb, bool NoteRequired, string DefaultNote,
        string NotificationTitle, string NotificationBody);

    private static readonly IReadOnlyDictionary<ShowcaseModerationAction, Rule> Rules = new Dictionary<ShowcaseModerationAction, Rule>
    {
        [ShowcaseModerationAction.Approve] = new(ShowcaseStatus.PendingReview, ShowcaseStatus.Published, "approved", false,
            "Approved for publication.", "Your showcase page was approved", "It is now visible to the E-HUB community."),
        [ShowcaseModerationAction.Reject] = new(ShowcaseStatus.PendingReview, ShowcaseStatus.Draft, "rejected", true,
            "Rejected.", "Your showcase page was not approved", "It was returned to draft and is not public."),
        [ShowcaseModerationAction.Hide] = new(ShowcaseStatus.Published, ShowcaseStatus.Hidden, "hidden", true,
            "Hidden by a reviewer.", "Your showcase page was hidden", "It is no longer visible to the community."),
        [ShowcaseModerationAction.Unhide] = new(ShowcaseStatus.Hidden, ShowcaseStatus.Published, "restored", false,
            "Restored by a reviewer.", "Your showcase page is visible again", "It is visible to the community again.")
    };

    private async Task<ShowcaseProject?> LoadOwnedAsync(Guid showcaseId, Guid userId, CancellationToken cancellationToken)
    {
        var showcase = await context.ShowcaseProjects.FirstOrDefaultAsync(item => item.Id == showcaseId, cancellationToken);
        return showcase is not null && await ShowcaseAccess.OwnsLineageAsync(context, showcase.ProjectLineageId, userId, cancellationToken)
            ? showcase
            : null;
    }

    private void Transition(ShowcaseProject showcase, ShowcaseStatus to, string? note, Guid actorId, DateTime now)
    {
        if (to == ShowcaseStatus.Hidden)
        {
            if (showcase.IsFeatured)
            {
                context.ShowcaseReviewHistories.Add(new ShowcaseReviewHistory
                {
                    ShowcaseProjectId = showcase.Id, FromStatus = showcase.Status, ToStatus = showcase.Status,
                    Note = "Featured selection removed because the showcase was hidden.", ActorId = actorId, CreatedAt = now
                });
            }
            showcase.IsFeatured = false;
            showcase.FeaturedAt = null;
        }
        context.ShowcaseReviewHistories.Add(new ShowcaseReviewHistory
        {
            ShowcaseProjectId = showcase.Id,
            FromStatus = showcase.Status,
            ToStatus = to,
            Note = note,
            ActorId = actorId,
            CreatedAt = now
        });
        showcase.Status = to;
        showcase.UpdatedBy = actorId;
    }

    private async Task<Result<ShowcaseDto>> SaveAsync(ShowcaseProject showcase, bool canEdit, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else moved the page between our read and write; nothing was applied, including the outbox row.
            context.ClearChanges();
            return Result.Failure<ShowcaseDto>(ErrorCodes.ShowcaseConcurrencyConflict,
                "The showcase page was changed by someone else. Reload and try again.");
        }

        var reloaded = await context.ShowcaseProjects.AsNoTracking()
            .Include(item => item.Links).Include(item => item.Media)
            .FirstAsync(item => item.Id == showcase.Id, cancellationToken);
        var profile = (await ShowcaseAccess.LatestProjectAsync(context, reloaded.ProjectLineageId, cancellationToken))?.Profile;
        return Result.Success(ShowcaseMapper.ToDto(reloaded, profile, canEdit));
    }

    private static Result<T> Denied<T>(string message) => Result.Failure<T>(ErrorCodes.ShowcaseAccessDenied, message);

    private static Result<T> NotFound<T>() => Result.Failure<T>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");

    private static Result<T> Invalid<T>(string message) => Result.Failure<T>(ErrorCodes.ShowcaseValidationError, message);

    private static Result<T> InvalidState<T>(string message) => Result.Failure<T>(ErrorCodes.ShowcaseInvalidState, message);
}
