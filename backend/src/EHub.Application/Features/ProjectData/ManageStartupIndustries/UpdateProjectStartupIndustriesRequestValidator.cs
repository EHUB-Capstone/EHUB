using EHub.Contracts.ProjectData;
using FluentValidation;

namespace EHub.Application.Features.ProjectData.ManageStartupIndustries;

public sealed class UpdateProjectStartupIndustriesRequestValidator : AbstractValidator<UpdateProjectStartupIndustriesRequest>
{
    public UpdateProjectStartupIndustriesRequestValidator()
    {
        RuleFor(request => request.StartupIndustryIds)
            .NotNull()
            .Must(ids => ids.Count is >= ProjectStartupIndustryRules.MinIndustries and <= ProjectStartupIndustryRules.MaxIndustries)
            .WithMessage($"Select between {ProjectStartupIndustryRules.MinIndustries} and {ProjectStartupIndustryRules.MaxIndustries} startup industries.");
        RuleForEach(request => request.StartupIndustryIds)
            .NotEqual(Guid.Empty)
            .WithMessage("A startup industry id is invalid.");
        RuleFor(request => request.RowVersion)
            .NotEmpty()
            .Matches("^[0-9]+$")
            .WithMessage("A valid rowVersion is required.");
    }
}
