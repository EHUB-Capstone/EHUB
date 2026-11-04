using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using FluentValidation;

namespace EHub.Application.Features.ProjectData.ManageCompetitions;

/// <summary>Checks the shape of the request; the rules that depend on the project (access, version, duplicates) live in the handler.</summary>
public sealed class UpdateProjectCompetitionsRequestValidator : AbstractValidator<UpdateProjectCompetitionsRequest>
{
    public UpdateProjectCompetitionsRequestValidator()
    {
        RuleFor(request => request.Entries)
            .NotNull()
            .Must(entries => entries.Count <= ProjectCompetitionNames.MaxEntriesPerProject)
            .WithMessage($"A project can have at most {ProjectCompetitionNames.MaxEntriesPerProject} competitions.");
        RuleForEach(request => request.Entries).ChildRules(entry =>
        {
            entry.RuleFor(item => item.Competition)
                .Must(value => ProjectCompetitionMapping.Canonicalize(value) is not null)
                .WithMessage("Competition must be one of the listed competitions.");
            entry.RuleFor(item => item.OtherName).MaximumLength(ProjectCompetitionNames.OtherNameMaxLength);
            entry.RuleFor(item => item.Year)
                .InclusiveBetween(ProjectCompetitionNames.MinYear, ProjectCompetitionNames.MaxYear)
                .WithMessage($"The year must be between {ProjectCompetitionNames.MinYear} and {ProjectCompetitionNames.MaxYear}.");
            entry.RuleFor(item => item.Stage)
                .Must(value => ProjectCompetitionMapping.ParseStage(value) is not null)
                .WithMessage($"Stage must be {string.Join(", ", ProjectCompetitionStageNames.All)}.");
        });
        RuleFor(request => request.RowVersion)
            .NotEmpty()
            .Matches("^[0-9]+$")
            .WithMessage("A valid rowVersion is required.");
    }
}
