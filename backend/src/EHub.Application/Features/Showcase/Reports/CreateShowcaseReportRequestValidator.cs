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
    }
}
