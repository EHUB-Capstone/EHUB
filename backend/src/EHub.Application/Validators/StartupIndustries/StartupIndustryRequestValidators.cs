using EHub.Contracts.StartupIndustries;
using FluentValidation;

namespace EHub.Application.Validators.StartupIndustries;

public sealed class CreateStartupIndustryRequestValidator : AbstractValidator<CreateStartupIndustryRequest>
{
    public CreateStartupIndustryRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Status).Must(IsValidStatus).WithMessage("Status must be active or inactive.");
    }

    private static bool IsValidStatus(string status) =>
        status.Equals("active", StringComparison.OrdinalIgnoreCase)
        || status.Equals("inactive", StringComparison.OrdinalIgnoreCase);
}

public sealed class UpdateStartupIndustryRequestValidator : AbstractValidator<UpdateStartupIndustryRequest>
{
    public UpdateStartupIndustryRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Status).Must(IsValidStatus).WithMessage("Status must be active or inactive.");
    }

    private static bool IsValidStatus(string status) =>
        status.Equals("active", StringComparison.OrdinalIgnoreCase)
        || status.Equals("inactive", StringComparison.OrdinalIgnoreCase);
}

public sealed class ChangeStartupIndustryStatusRequestValidator : AbstractValidator<ChangeStartupIndustryStatusRequest>
{
    public ChangeStartupIndustryStatusRequestValidator()
    {
        RuleFor(request => request.Status).Must(status =>
                status.Equals("active", StringComparison.OrdinalIgnoreCase)
                || status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be active or inactive.");
    }
}
