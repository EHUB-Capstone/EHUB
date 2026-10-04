using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using FluentValidation;

namespace EHub.Application.Features.ProjectData.ManageAchievements;

public sealed class UpdateProjectAchievementsRequestValidator : AbstractValidator<UpdateProjectAchievementsRequest>
{
    public UpdateProjectAchievementsRequestValidator()
    {
        RuleFor(request => request.Achievements)
            .NotNull()
            .Must(items => items.Count <= ProjectAchievementNames.All.Count)
            .WithMessage("Too many achievements were supplied.");
        RuleForEach(request => request.Achievements)
            .Must(value => ProjectAchievementMapping.Canonicalize(value) is not null)
            .WithMessage("Achievement must be Potential, Funded or Awarded.");
        RuleFor(request => request.Note)
            .MaximumLength(ProjectAchievementMapping.NoteMaxLength);
        RuleFor(request => request)
            .Must(request => string.IsNullOrWhiteSpace(request.Note) || request.Achievements.Count > 0)
            .WithName(nameof(UpdateProjectAchievementsRequest.Note))
            .WithMessage("A note needs at least one achievement.");
        RuleFor(request => request.RowVersion)
            .NotEmpty()
            .Matches("^[0-9]+$")
            .WithMessage("A valid rowVersion is required.");
    }
}
