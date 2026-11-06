using EHub.Contracts.Showcase;
using EHub.Domain.Enums;
using FluentValidation;

namespace EHub.Application.Features.Showcase.Discovery;

public sealed class GetShowcasesRequestValidator : AbstractValidator<GetShowcasesRequest>
{
    public GetShowcasesRequestValidator()
    {
        RuleFor(request => request.Search).MaximumLength(100);
        RuleFor(request => request.Industry).MaximumLength(100);
        RuleFor(request => request.Stage)
            .Must(value => Enum.GetNames<ShowcaseDevelopmentStage>().Contains(value?.Trim(), StringComparer.OrdinalIgnoreCase))
            .When(request => !string.IsNullOrWhiteSpace(request.Stage));
        RuleFor(request => request.SemesterId).NotEqual(Guid.Empty).When(request => request.SemesterId.HasValue);
        RuleFor(request => request.Sort).Must(value =>
            string.Equals(value?.Trim(), "newest", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value?.Trim(), "popular", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value?.Trim(), "trending", StringComparison.OrdinalIgnoreCase));
        RuleFor(request => request.Page).InclusiveBetween(1, int.MaxValue / 50);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 50);
    }
}
