using EHub.Contracts.Showcase;
using FluentValidation;

namespace EHub.Application.Features.Showcase.Reports;

public sealed class ResolveShowcaseReportRequestValidator : AbstractValidator<ResolveShowcaseReportRequest>
{
    public ResolveShowcaseReportRequestValidator()
    {
        RuleFor(request => request.Status)
            .Must(status => string.Equals(status, "Dismissed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "ActionTaken", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be Dismissed or ActionTaken.");
        RuleFor(request => request.ResolutionNote).MaximumLength(1000);
    }
}
