using EHub.Contracts.Showcase;
using EHub.Domain.Enums;
using FluentValidation;

namespace EHub.Application.Features.Showcase.Reports;

public sealed class CreateShowcaseReportRequestValidator : AbstractValidator<CreateShowcaseReportRequest>
{
    public CreateShowcaseReportRequestValidator()
    {
        RuleFor(request => request.CommentId).Must(id => id is null || id != Guid.Empty)
            .WithMessage("Comment id is invalid.");
        RuleFor(request => request.Reason)
            .Must(reason => Enum.GetNames<ShowcaseReportReason>().Contains(reason, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Reason must be Spam, Harassment, InappropriateContent, CopyrightViolation or Other.");
        RuleFor(request => request.Note).MaximumLength(1000);
        RuleFor(request => request.Category).Must(category => category is null || Enum.GetNames<ShowcaseViolationCategory>().Contains(category))
            .WithMessage("Select a valid violation category.");
        RuleFor(request => request).Must(request => request.Category is null ||
                (Enum.TryParse<ShowcaseReportReason>(request.Reason, true, out var reason)
                 && Enum.TryParse<ShowcaseViolationCategory>(request.Category, out var category)
                 && ShowcaseViolationCategories.ToReason(category) == reason))
            .WithMessage("The violation category must match the report reason.");
    }
}
