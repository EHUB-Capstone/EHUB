using EHub.Application.Common.Interfaces.Persistence;
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
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Reason).Must(x => x is "Spam" or "Harassment" or "InappropriateContent");
    }
}

public sealed class ShowcaseModerationRuleHandler(IApplicationDbContext context, IValidator<SaveShowcaseModerationRuleRequest> validator)
{
    public async Task<Result<IReadOnlyCollection<ShowcaseModerationRuleDto>>> ListAsync(string role, CancellationToken token)
    {
        if (!ShowcaseAccess.IsAdmin(role)) return Denied<IReadOnlyCollection<ShowcaseModerationRuleDto>>();
        var rules = await context.ShowcaseModerationRules.AsNoTracking().OrderBy(x => x.Phrase).ToListAsync(token);
        return Result.Success<IReadOnlyCollection<ShowcaseModerationRuleDto>>(rules.Select(ToDto).ToArray());
    }

    public async Task<Result<ShowcaseModerationRuleDto>> SaveAsync(Guid? id, SaveShowcaseModerationRuleRequest request, string role, CancellationToken token)
    {
        if (!ShowcaseAccess.IsAdmin(role)) return Denied<ShowcaseModerationRuleDto>();
        var validation = await validator.ValidateAsync(request, token);
        if (!validation.IsValid) return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseValidationError, validation.Errors[0].ErrorMessage);
        var rule = id.HasValue ? await context.ShowcaseModerationRules.SingleOrDefaultAsync(x => x.Id == id, token) : new ShowcaseModerationRule();
        if (rule is null) return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseNotFound, "The moderation rule was not found.");
        if (id.HasValue && (!request.RowVersion.HasValue || request.RowVersion.Value != rule.Version))
            return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseConcurrencyConflict, "Reload the moderation rules before saving.");
        if (!id.HasValue && await context.ShowcaseModerationRules.CountAsync(token) >= 200)
            return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseValidationError, "At most 200 moderation rules are supported.");
        var requestedReason = Enum.Parse<ShowcaseReportReason>(request.Reason);
        rule.Category = request.Category is not null
            ? Enum.Parse<ShowcaseViolationCategory>(request.Category)
            : id.HasValue && requestedReason == rule.Reason ? rule.Category : ShowcaseViolationCategories.FromReason(requestedReason);
        rule.Phrase = request.Phrase.Trim();
        rule.Description = request.Description.Trim();
        rule.Reason = ShowcaseViolationCategories.ToReason(rule.Category);
        rule.IsEnabled = request.IsEnabled;
        if (!id.HasValue) context.ShowcaseModerationRules.Add(rule);
        try { await context.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException)
        {
            context.ClearChanges();
            return Result.Failure<ShowcaseModerationRuleDto>(ErrorCodes.ShowcaseConcurrencyConflict, "The moderation rule changed. Reload and try again.");
        }
        return Result.Success(ToDto(rule));
    }

    private static ShowcaseModerationRuleDto ToDto(ShowcaseModerationRule x) => new()
    { Id = x.Id, Phrase = x.Phrase, Description = x.Description, Reason = x.Reason.ToString(), Category = x.Category.ToString(), IsEnabled = x.IsEnabled, RowVersion = x.Version };
    private static Result<T> Denied<T>() => Result.Failure<T>(ErrorCodes.ShowcaseAccessDenied, "Only administrators can manage automatic moderation rules.");
}
