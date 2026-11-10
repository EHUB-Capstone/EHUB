using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Moderation;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Reports;

public sealed class ShowcaseReportHandler(
    IApplicationDbContext context,
    IDateTimeProvider time,
    IShowcaseModerationHandler moderation,
    IValidator<CreateShowcaseReportRequest> createValidator,
    IValidator<ResolveShowcaseReportRequest> resolveValidator) : IShowcaseReportHandler
{
    public async Task<Result<ShowcaseReportReceiptDto>> CreateAsync(
        Guid showcaseId, CreateShowcaseReportRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !await context.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken))
            return Result.Failure<ShowcaseReportReceiptDto>(ErrorCodes.CommonUnauthorizedError, "Authentication is required.");
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<ShowcaseReportReceiptDto>(ErrorCodes.ShowcaseValidationError, validation.Errors[0].ErrorMessage);

        var showcase = await context.ShowcaseProjects.AsNoTracking()
            .Where(page => page.Id == showcaseId && page.Status == ShowcaseStatus.Published)
            .Select(page => new { page.ProjectLineageId }).SingleOrDefaultAsync(cancellationToken);
        if (showcase is null)
            return Result.Failure<ShowcaseReportReceiptDto>(ErrorCodes.ShowcaseNotFound, "The showcase page was not found.");
        if (request.CommentId is { } commentId)
        {
            var comment = await context.ShowcaseComments.AsNoTracking()
                .Where(item => item.Id == commentId && item.ShowcaseProjectId == showcaseId)
                .Select(item => new { item.AuthorUserId }).SingleOrDefaultAsync(cancellationToken);
            if (comment is null)
                return Result.Failure<ShowcaseReportReceiptDto>(ErrorCodes.ShowcaseCommentNotFound, "The comment was not found.");
            if (comment.AuthorUserId == userId)
                return Result.Failure<ShowcaseReportReceiptDto>(ErrorCodes.ShowcaseAccessDenied, "You cannot report your own comment.");
        }
        else if (await ShowcaseAccess.OwnsLineageAsync(context, showcase.ProjectLineageId, userId, cancellationToken))
        {
            return Result.Failure<ShowcaseReportReceiptDto>(ErrorCodes.ShowcaseAccessDenied, "Team members cannot report their own showcase.");
        }

        if (await HasReportAsync(showcaseId, request.CommentId, userId, cancellationToken))
            return Duplicate();
        var report = new ShowcaseReport
        {
            ShowcaseProjectId = showcaseId, CommentId = request.CommentId, ReporterUserId = userId,
            Reason = Enum.Parse<ShowcaseReportReason>(request.Reason, true),
            Note = Clean(request.Note), CreatedAt = time.UtcNow
        };
        context.ShowcaseReports.Add(report);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A unique index also enforces the rule when two requests pass the initial check together.
            context.ClearChanges();
            if (await HasReportAsync(showcaseId, request.CommentId, userId, cancellationToken))
                return Duplicate();
            throw;
        }
        return Result.Success(new ShowcaseReportReceiptDto
        {
            Id = report.Id, ShowcaseProjectId = showcaseId, CommentId = report.CommentId,
            Status = report.Status.ToString(), CreatedAt = report.CreatedAt
        });
    }

    public async Task<Result<PagedResponse<ShowcaseReportDto>>> ListAsync(
        string? status, int page, int pageSize, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsModerator(role))
            return Result.Failure<PagedResponse<ShowcaseReportDto>>(ErrorCodes.ShowcaseAccessDenied, "Only lecturers and administrators can review reports.");
        status = string.IsNullOrWhiteSpace(status) ? nameof(ShowcaseReportStatus.Open) : status;
        if (!Enum.GetNames<ShowcaseReportStatus>().Contains(status, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<PagedResponse<ShowcaseReportDto>>(ErrorCodes.ShowcaseValidationError, "Status must be Open, Dismissed or ActionTaken.");
        var state = Enum.Parse<ShowcaseReportStatus>(status, true);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        if (page > int.MaxValue / pageSize)
            return Result.Failure<PagedResponse<ShowcaseReportDto>>(ErrorCodes.ShowcaseValidationError, "Page is too large.");
        var query = ScopedReports(userId, role).AsNoTracking().Where(report => report.Status == state);
        var total = await query.CountAsync(cancellationToken);
        var reports = await query.OrderBy(report => report.CreatedAt).ThenBy(report => report.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Result.Success(new PagedResponse<ShowcaseReportDto>
        {
            Items = reports.Select(ToDto).ToArray(), TotalItems = total, PageIndex = page,
            PageSize = pageSize, TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    public async Task<Result<ShowcaseReportDto>> ResolveAsync(
        Guid reportId, ResolveShowcaseReportRequest request, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!ShowcaseAccess.IsModerator(role))
            return Result.Failure<ShowcaseReportDto>(ErrorCodes.ShowcaseAccessDenied, "Only lecturers and administrators can resolve reports.");
        var validation = await resolveValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<ShowcaseReportDto>(ErrorCodes.ShowcaseValidationError, validation.Errors[0].ErrorMessage);
        var report = await ScopedReports(userId, role).SingleOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null)
            return Result.Failure<ShowcaseReportDto>(ErrorCodes.ShowcaseReportNotFound, "The report was not found.");
        if (report.Status != ShowcaseReportStatus.Open)
            return Result.Failure<ShowcaseReportDto>(ErrorCodes.ShowcaseInvalidState, "This report has already been resolved.");
        var targetStatus = Enum.Parse<ShowcaseReportStatus>(request.Status, true);
        var showcase = await context.ShowcaseProjects.SingleAsync(item => item.Id == report.ShowcaseProjectId, cancellationToken);
        if (targetStatus == ShowcaseReportStatus.ActionTaken && showcase.Status is not (ShowcaseStatus.Published or ShowcaseStatus.Hidden))
            return Result.Failure<ShowcaseReportDto>(ErrorCodes.ShowcaseInvalidState, "Only a published or already hidden showcase can be actioned.");

        report.Status = targetStatus;
        report.ResolvedAt = time.UtcNow;
        report.ResolvedByUserId = userId;
        report.ResolutionNote = Clean(request.ResolutionNote);
        if (targetStatus == ShowcaseReportStatus.ActionTaken && showcase.Status == ShowcaseStatus.Published)
        {
            // The moderation handler saves this tracked report, the hide, audit history and outbox together.
            // Private report text never reaches the team's ReviewNote, history or notification.
            var hidden = await moderation.DecideAsync(showcase.Id, ShowcaseModerationAction.Hide,
                "Hidden after a content report was reviewed.", userId, role, cancellationToken);
            if (hidden.IsFailure)
            {
                context.ClearChanges();
                return Result.Failure<ShowcaseReportDto>(hidden.Error);
            }
        }
        else
        {
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                context.ClearChanges();
                return Result.Failure<ShowcaseReportDto>(ErrorCodes.ShowcaseConcurrencyConflict, "The report was changed by another reviewer. Reload and try again.");
            }
        }
        return Result.Success(ToDto(report));
    }

    private IQueryable<ShowcaseReport> ScopedReports(Guid userId, string role)
    {
        var showcases = ShowcaseAccess.ModeratableShowcases(context, userId, role);
        return context.ShowcaseReports.Where(report => showcases.Any(showcase => showcase.Id == report.ShowcaseProjectId));
    }

    private Task<bool> HasReportAsync(Guid showcaseId, Guid? commentId, Guid userId, CancellationToken cancellationToken) =>
        context.ShowcaseReports.AsNoTracking().AnyAsync(report => report.ShowcaseProjectId == showcaseId
            && report.CommentId == commentId && report.ReporterUserId == userId, cancellationToken);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<ShowcaseReportReceiptDto> Duplicate() =>
        Result.Failure<ShowcaseReportReceiptDto>(ErrorCodes.ShowcaseReportAlreadyExists, "You have already reported this content.");

    private static ShowcaseReportDto ToDto(ShowcaseReport report) => new()
    {
        Id = report.Id, ShowcaseProjectId = report.ShowcaseProjectId, CommentId = report.CommentId,
        Reason = report.Reason.ToString(), Note = report.Note, Status = report.Status.ToString(),
        CreatedAt = report.CreatedAt, ResolvedAt = report.ResolvedAt, ResolvedByUserId = report.ResolvedByUserId,
        ResolutionNote = report.ResolutionNote
    };
}
