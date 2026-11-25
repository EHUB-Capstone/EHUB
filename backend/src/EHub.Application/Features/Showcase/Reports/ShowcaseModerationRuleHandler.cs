using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Reports;

public sealed class SaveShowcaseModerationRuleRequestValidator : AbstractValidator<SaveShowcaseModerationRuleRequest>
{
    public SaveShowcaseModerationRuleRequestValidator()
    {
        RuleFor(x => x.Phrase).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(120)
            .Must(x => ShowcaseProposalText.Canonical(x).Any(char.IsLetter))
            .WithMessage("Enter a word or phrase containing at least one letter.");
        RuleFor(x => x.Category).Must(x => x is null || Enum.GetNames<ShowcaseViolationCategory>().Contains(x))
            .WithMessage("Select a valid violation category.");
        RuleFor(x => x.Category).Must(x => x is null || (Enum.TryParse<ShowcaseViolationCategory>(x, out var category)
                && ShowcaseViolationCategories.SupportsPhraseBlocking(category)))
            .WithMessage("This category requires human review and cannot be used for keyword blocking.");
        RuleFor(x => x.Status).Must(x => x is null || Enum.GetNames<ShowcaseModerationRuleStatus>().Contains(x))
            .WithMessage("Status must be Active or Inactive.");
        RuleFor(x => x).Must(x => x.Status is null || !x.IsEnabled.HasValue ||
                (Enum.TryParse<ShowcaseModerationRuleStatus>(x.Status, out var status)
                 && (status == ShowcaseModerationRuleStatus.Active) == x.IsEnabled.Value))
            .WithMessage("Status and the legacy enabled flag must agree.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Reason).Must(x => x is "Spam" or "Harassment" or "InappropriateContent");
    }
}

public sealed class ShowcaseModerationRuleHandler(IApplicationDbContext context, IValidator<SaveShowcaseModerationRuleRequest> validator,
    IShowcaseRuleProposalTransaction transaction, IDateTimeProvider time)
{
    public async Task<Result<IReadOnlyCollection<ShowcaseModerationRuleDto>>> ListAsync(string role, CancellationToken token)
    {
        if (!ShowcaseAccess.IsAdmin(role)) return Denied<IReadOnlyCollection<ShowcaseModerationRuleDto>>();
        var rules = await context.ShowcaseModerationRules.AsNoTracking().OrderBy(x => x.Phrase).ToListAsync(token);
        return Result.Success(await ToDtosAsync(rules, token));
    }

    public async Task<Result<ShowcaseModerationRuleDto>> SaveAsync(Guid? id, SaveShowcaseModerationRuleRequest request, Guid userId, string role, CancellationToken token)
    {
        if (!ShowcaseAccess.IsAdmin(role)) return Denied<ShowcaseModerationRuleDto>();
        if (userId == Guid.Empty || !await context.Users.AsNoTracking().AnyAsync(x => x.Id == userId, token))
            return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.CommonUnauthorizedError, "Authentication is required.");
        try { return await transaction.ExecuteAsync(ct => SaveCoreAsync(id, request, userId, ct), token); }
        catch (SerializableTransactionConflictException)
        {
            context.ClearChanges();
            return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseConcurrencyConflict, "The rules changed. Reload before saving.");
        }
    }

    private async Task<Result<ShowcaseModerationRuleDto>> SaveCoreAsync(Guid? id, SaveShowcaseModerationRuleRequest request, Guid userId, CancellationToken token)
    {
        var validation = await validator.ValidateAsync(request, token);
        if (!validation.IsValid) return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseValidationError, validation.Errors[0].ErrorMessage);
        var rule = id.HasValue ? await context.ShowcaseModerationRules.SingleOrDefaultAsync(x => x.Id == id, token) : new ShowcaseModerationRule();
        if (rule is null) return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseNotFound, "The moderation rule was not found.");
        if (id.HasValue && (!request.RowVersion.HasValue || request.RowVersion.Value != rule.Version))
            return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseConcurrencyConflict, "Reload the moderation rules before saving.");
        if (!id.HasValue && await context.ShowcaseModerationRules.CountAsync(token) >= 200)
            return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseValidationError, "At most 200 moderation rules are supported.");
        var phrase = ShowcaseProposalText.Canonical(request.Phrase);
        var otherPhrases = await context.ShowcaseModerationRules.AsNoTracking().Where(x => x.Id != rule.Id).Select(x => x.Phrase).ToListAsync(token);
        if (otherPhrases.Any(x => ShowcaseProposalText.Canonical(x) == phrase))
            return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseRuleProposalAlreadyExists, "This word or phrase already exists. Edit its current rule instead.");
        if (!id.HasValue || ShowcaseProposalText.Canonical(rule.Phrase) != phrase)
        {
            var phraseKey = ShowcaseContentFilter.Hash(phrase);
            if (await context.ShowcaseModerationRuleProposals.AsNoTracking().AnyAsync(x => x.PhraseKey == phraseKey
                    && x.Status == ShowcaseRuleProposalStatus.Pending, token))
                return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseRuleProposalAlreadyExists, "Review the pending proposal for this phrase first.");
        }
        var requestedReason = Enum.Parse<ShowcaseReportReason>(request.Reason);
        rule.Category = request.Category is not null
            ? Enum.Parse<ShowcaseViolationCategory>(request.Category)
            : id.HasValue && requestedReason == rule.Reason ? rule.Category : ShowcaseViolationCategories.FromReason(requestedReason);
        rule.Phrase = request.Phrase.Trim();
        rule.Description = request.Description.Trim();
        rule.Reason = ShowcaseViolationCategories.ToReason(rule.Category);
        rule.Status = request.Status is not null ? Enum.Parse<ShowcaseModerationRuleStatus>(request.Status)
            : request.IsEnabled.HasValue ? request.IsEnabled.Value ? ShowcaseModerationRuleStatus.Active : ShowcaseModerationRuleStatus.Inactive
            : rule.Status;
        var now = time.UtcNow;
        if (!id.HasValue) { rule.CreatedByUserId = userId; rule.CreatedAtUtc = now; }
        rule.UpdatedByUserId = userId;
        rule.UpdatedAtUtc = now;
        if (!id.HasValue) context.ShowcaseModerationRules.Add(rule);
        try { await context.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException)
        {
            context.ClearChanges();
            return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseConcurrencyConflict, "The moderation rule changed. Reload and try again.");
        }
        return Result.Success((await ToDtosAsync([rule], token)).Single());
    }

    private async Task<IReadOnlyCollection<ShowcaseModerationRuleDto>> ToDtosAsync(IReadOnlyCollection<ShowcaseModerationRule> rules, CancellationToken token)
    {
        var ids = rules.SelectMany(x => new[] { x.CreatedByUserId, x.UpdatedByUserId }).OfType<Guid>().Distinct().ToArray();
        var names = await context.Users.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, token);
        return rules.Select(x => new ShowcaseModerationRuleDto
        {
            Id = x.Id, Phrase = x.Phrase, Description = x.Description, Reason = x.Reason.ToString(), Category = x.Category.ToString(),
            Status = x.Status.ToString(), RowVersion = x.Version,
            CreatedByUserId = x.CreatedByUserId, CreatedAtUtc = x.CreatedAtUtc,
            CreatedByName = x.CreatedByUserId is { } creator ? names.GetValueOrDefault(creator) : null,
            UpdatedByUserId = x.UpdatedByUserId, UpdatedAtUtc = x.UpdatedAtUtc,
            UpdatedByName = x.UpdatedByUserId is { } editor ? names.GetValueOrDefault(editor) : null
        }).ToArray();
    }
    private static Result<T> Denied<T>() => Result.Failure<T>(ErrorCodes.ShowcaseAccessDenied, "Only administrators can manage automatic moderation rules.");
}
