using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Reports;

public sealed class CreateShowcaseRuleProposalValidator : AbstractValidator<CreateShowcaseRuleProposalRequest>
{
    public CreateShowcaseRuleProposalValidator()
    {
        RuleFor(x => x.Phrase).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(120)
            .Must(x => ShowcaseProposalText.Canonical(x).Any(char.IsLetter));
        RuleFor(x => x.Category).Must(x => Enum.GetNames<ShowcaseViolationCategory>().Contains(x));
    }
}

public sealed class ShowcaseRuleProposalHandler(IApplicationDbContext context, IDateTimeProvider time,
    IValidator<CreateShowcaseRuleProposalRequest> validator, IShowcaseRuleProposalTransaction transaction)
{
    public Task<Result<ShowcaseRuleProposalDto>> CreateAsync(Guid reportId, CreateShowcaseRuleProposalRequest request,
        Guid userId, string role, CancellationToken token = default) =>
        WriteAsync(ct => CreateCoreAsync(reportId, request, userId, role, ct), request.Phrase, token);

    private async Task<Result<ShowcaseRuleProposalDto>> CreateCoreAsync(Guid reportId, CreateShowcaseRuleProposalRequest request,
        Guid userId, string role, CancellationToken token = default)
    {
        if (!await Authenticated(userId, token)) return Unauthorized<ShowcaseRuleProposalDto>();
        if (!ShowcaseAccess.IsModerator(role)) return Denied<ShowcaseRuleProposalDto>();
        var validation = await validator.ValidateAsync(request, token);
        if (!validation.IsValid) return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseValidationError, validation.Errors[0].ErrorMessage);
        var report = await ShowcaseAccess.ModeratableReports(context, userId, role).AsNoTracking().SingleOrDefaultAsync(x => x.Id == reportId
            && x.Source == ShowcaseReportSource.User, token);
        if (report is null) return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseReportNotFound, "The report was not found.");
        if (report.CommentId is not { } commentId || report.Status == ShowcaseReportStatus.Dismissed)
            return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseInvalidState, "Only a reported comment that has not been dismissed can be used.");
        var comment = await context.ShowcaseComments.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == commentId && x.ShowcaseProjectId == report.ShowcaseProjectId).Select(x => x.Content).SingleOrDefaultAsync(token);
        if (comment is null) return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseCommentNotFound, "The source comment is no longer available.");
        if (!ShowcaseProposalText.IsInComment(comment, request.Phrase))
            return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseRuleProposalInvalidPhrase, "Choose a complete word or phrase from the reported comment, preserving Vietnamese accents.");
        var phrase = ShowcaseProposalText.Canonical(request.Phrase);
        var key = ShowcaseContentFilter.Hash(phrase);
        if (await ExistingRule(phrase, token)) return Duplicate();
        var isAdmin = ShowcaseAccess.IsAdmin(role);
        var pending = await context.ShowcaseModerationRuleProposals.SingleOrDefaultAsync(x => x.PhraseKey == key && x.Status == ShowcaseRuleProposalStatus.Pending, token);
        if (pending is not null && !isAdmin) return Duplicate();
        if (!isAdmin && await context.ShowcaseModerationRuleProposals.CountAsync(x => x.Status == ShowcaseRuleProposalStatus.Pending, token) >= 200)
            return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseValidationError, "The proposal queue is full. Please try again after it has been reviewed.");
        var proposal = pending ?? new ShowcaseModerationRuleProposal
        {
            SourceReportId = report.Id, ShowcaseProjectId = report.ShowcaseProjectId, CommentId = commentId,
            Phrase = phrase, PhraseKey = key, Category = Enum.Parse<ShowcaseViolationCategory>(request.Category),
            ProposedByUserId = userId, ProposedAtUtc = time.UtcNow
        };
        if (isAdmin)
        {
            var category = Enum.Parse<ShowcaseViolationCategory>(request.Category);
            var approval = await AddApprovedRuleAsync(proposal, category, token);
            if (approval.IsFailure) return Result.Failure<ShowcaseRuleProposalDto>(approval.Error);
            proposal.Category = category;
            MarkReviewed(proposal, ShowcaseRuleProposalStatus.Approved, userId);
        }
        else
            ShowcaseOutbox.EnqueueNotification(context, proposal.ShowcaseProjectId, [], "New language rule proposal",
                "A proposed blocked phrase needs administrator review.", "/showcase/moderation?tab=reports", time.UtcNow, notifyAdministrators: true);
        if (pending is null) context.ShowcaseModerationRuleProposals.Add(proposal);
        await context.SaveChangesAsync(token);
        var dto = ToDto(proposal); dto.CommentContent = comment;
        return Result.Success(dto);
    }

    public async Task<Result<PagedResponse<ShowcaseRuleProposalDto>>> ListAsync(string? status, int page, int pageSize,
        Guid userId, string role, CancellationToken token = default)
    {
        if (!await Authenticated(userId, token)) return Unauthorized<PagedResponse<ShowcaseRuleProposalDto>>();
        if (!ShowcaseAccess.IsAdmin(role)) return Denied<PagedResponse<ShowcaseRuleProposalDto>>();
        status = string.IsNullOrWhiteSpace(status) ? "Pending" : status;
        if (!Enum.GetNames<ShowcaseRuleProposalStatus>().Contains(status))
            return Result.Failure<PagedResponse<ShowcaseRuleProposalDto>>(ErrorCodes.ShowcaseValidationError, "Select a valid proposal status.");
        var state = Enum.Parse<ShowcaseRuleProposalStatus>(status);
        page = Math.Max(1, page); pageSize = pageSize <= 0 ? 20 : Math.Clamp(pageSize, 1, 50);
        if (page > int.MaxValue / pageSize) return Result.Failure<PagedResponse<ShowcaseRuleProposalDto>>(ErrorCodes.ShowcaseValidationError, "Page is too large.");
        var query = VisibleProposals().AsNoTracking().Where(x => x.Status == state);
        var count = await query.CountAsync(token);
        var rows = await query.OrderBy(x => x.ProposedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(token);
        var ids = rows.Where(x => x.CommentId.HasValue).Select(x => x.CommentId!.Value).ToArray();
        var comments = await context.ShowcaseComments.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Content, token);
        var items = rows.Select(ToDto).ToArray();
        foreach (var item in items) item.CommentContent = item.CommentId is { } id ? comments.GetValueOrDefault(id) : null;
        return Result.Success(new PagedResponse<ShowcaseRuleProposalDto> { Items = items, PageIndex = page, PageSize = pageSize, TotalItems = count, TotalPages = (int)Math.Ceiling(count / (double)pageSize) });
    }

    public Task<Result<ShowcaseRuleProposalDto>> ReviewAsync(Guid id, ReviewShowcaseRuleProposalRequest request,
        Guid userId, string role, CancellationToken token = default) =>
        WriteAsync(ct => ReviewCoreAsync(id, request, userId, role, ct), null, token);

    private async Task<Result<ShowcaseRuleProposalDto>> ReviewCoreAsync(Guid id, ReviewShowcaseRuleProposalRequest request,
        Guid userId, string role, CancellationToken token = default)
    {
        if (!await Authenticated(userId, token)) return Unauthorized<ShowcaseRuleProposalDto>();
        if (!ShowcaseAccess.IsAdmin(role)) return Denied<ShowcaseRuleProposalDto>();
        if (request.Status is not ("Approved" or "Rejected"))
            return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseValidationError, "Choose Approved or Rejected.");
        var proposal = await VisibleProposals().SingleOrDefaultAsync(x => x.Id == id, token);
        if (proposal is null) return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseRuleProposalNotFound, "The proposal was not found.");
        var current = await context.ShowcaseModerationRuleProposals.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Status, x.Version }).SingleAsync(token);
        if (current.Status != ShowcaseRuleProposalStatus.Pending)
            return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseInvalidState, "This proposal has already been reviewed.");
        if (!request.RowVersion.HasValue || request.RowVersion != current.Version || proposal.Version != current.Version)
            return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseConcurrencyConflict, "Reload the proposals before reviewing.");
        if (request.Status == "Approved")
        {
            var approval = await AddApprovedRuleAsync(proposal, proposal.Category, token);
            if (approval.IsFailure) return Result.Failure<ShowcaseRuleProposalDto>(approval.Error);
        }
        MarkReviewed(proposal, Enum.Parse<ShowcaseRuleProposalStatus>(request.Status), userId);
        await context.SaveChangesAsync(token);
        return Result.Success(ToDto(proposal));
    }

    private async Task<Result> AddApprovedRuleAsync(ShowcaseModerationRuleProposal proposal, ShowcaseViolationCategory category, CancellationToken token)
    {
        var content = await context.ShowcaseComments.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == proposal.CommentId && x.ShowcaseProjectId == proposal.ShowcaseProjectId).Select(x => x.Content).SingleOrDefaultAsync(token);
        if (content is null || !ShowcaseProposalText.IsInComment(content, proposal.Phrase))
            return Result.Failure(ErrorCodes.ShowcaseConcurrencyConflict, "The source comment changed or is no longer available. Review it again.");
        if (await ExistingRule(proposal.Phrase, token))
            return Result.Failure(ErrorCodes.ShowcaseRuleProposalAlreadyExists, "This word or phrase already exists.");
        if (await context.ShowcaseModerationRules.CountAsync(token) >= 200)
            return Result.Failure(ErrorCodes.ShowcaseValidationError, "At most 200 moderation rules are supported.");
        var rule = new ShowcaseModerationRule { Phrase = proposal.Phrase, Category = category,
            Reason = ShowcaseViolationCategories.ToReason(category), IsEnabled = true,
            Description = "Approved from a reported comment / Đã duyệt từ báo cáo bình luận." };
        context.ShowcaseModerationRules.Add(rule);
        proposal.CreatedRuleId = rule.Id;
        return Result.Success();
    }

    private void MarkReviewed(ShowcaseModerationRuleProposal proposal, ShowcaseRuleProposalStatus status, Guid userId)
    {
        proposal.Status = status;
        proposal.ReviewedAtUtc = time.UtcNow; proposal.ReviewedByUserId = userId;
        if (proposal.ProposedByUserId is { } proposer && proposer != userId)
            ShowcaseOutbox.EnqueueNotification(context, proposal.ShowcaseProjectId, [proposer], "Language rule proposal reviewed",
                status == ShowcaseRuleProposalStatus.Approved ? "An administrator approved your language rule proposal." : "An administrator declined your language rule proposal.",
                "/showcase/moderation?tab=reports", time.UtcNow);
    }

    private async Task<Result<ShowcaseRuleProposalDto>> WriteAsync(Func<CancellationToken, Task<Result<ShowcaseRuleProposalDto>>> operation,
        string? phrase, CancellationToken token)
    {
        // Serialize the duplicate check and dictionary insert, including two administrators adding the same phrase.
        // The pending entry, enabled rule, review audit and any Outbox notice commit together.
        try { return await transaction.ExecuteAsync(operation, token); }
        catch (SerializableTransactionConflictException)
        {
            context.ClearChanges();
            return Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseConcurrencyConflict, "Another reviewer changed this proposal. Reload and try again.");
        }
        catch (DbUpdateException) when (phrase is not null)
        {
            context.ClearChanges();
            var canonical = ShowcaseProposalText.Canonical(phrase);
            var key = ShowcaseContentFilter.Hash(canonical);
            if (await ExistingRule(canonical, token) || await context.ShowcaseModerationRuleProposals.AsNoTracking()
                    .AnyAsync(x => x.PhraseKey == key && x.Status == ShowcaseRuleProposalStatus.Pending, token)) return Duplicate();
            throw;
        }
    }

    // Administrator-only queue keeps proposals reviewable even if the source page was archived.
    private IQueryable<ShowcaseModerationRuleProposal> VisibleProposals() => context.ShowcaseModerationRuleProposals;
    private Task<bool> Authenticated(Guid userId, CancellationToken token) => userId == Guid.Empty ? Task.FromResult(false)
        : context.Users.AsNoTracking().AnyAsync(x => x.Id == userId, token);
    private async Task<bool> ExistingRule(string phrase, CancellationToken token) => (await context.ShowcaseModerationRules.AsNoTracking().Select(x => x.Phrase).ToListAsync(token))
        .Any(x => ShowcaseProposalText.Canonical(x) == phrase);
    private static Result<ShowcaseRuleProposalDto> Duplicate() => Result.Failure<ShowcaseRuleProposalDto>(ErrorCodes.ShowcaseRuleProposalAlreadyExists, "This word or phrase already exists or is awaiting review.");
    private static Result<T> Unauthorized<T>() => Result.Failure<T>(ErrorCodes.CommonUnauthorizedError, "Authentication is required.");
    private static Result<T> Denied<T>() => Result.Failure<T>(ErrorCodes.ShowcaseAccessDenied, "You do not have permission to manage these proposals.");
    private static ShowcaseRuleProposalDto ToDto(ShowcaseModerationRuleProposal x) => new() { Id = x.Id, ShowcaseProjectId = x.ShowcaseProjectId,
        SourceReportId = x.SourceReportId, CommentId = x.CommentId, Phrase = x.Phrase, Category = x.Category.ToString(), Status = x.Status.ToString(),
        ProposedAtUtc = x.ProposedAtUtc, ReviewedAtUtc = x.ReviewedAtUtc, CreatedRuleId = x.CreatedRuleId, RowVersion = x.Version };
}
