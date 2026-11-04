using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using FluentValidation;

namespace EHub.Application.Features.ProjectData.GetProjectData;

public sealed class GetProjectDataRequestValidator : AbstractValidator<GetProjectDataRequest>
{
    private static readonly string TooManyValuesMessage =
        $"A filter accepts at most {ProjectDataQuery.MaxFilterValues} values.";

    private static bool HaveAtMostMaxValues<T>(T[]? values) =>
        values is null || values.Length <= ProjectDataQuery.MaxFilterValues;

    public GetProjectDataRequestValidator()
    {
        RuleFor(request => request.PageIndex).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize)
            .Must(size => ProjectDataQuery.AllowedPageSizes.Contains(size))
            .WithMessage($"Page size must be one of: {string.Join(", ", ProjectDataQuery.AllowedPageSizes)}.");
        RuleFor(request => request.Search).MaximumLength(ProjectDataQuery.MaxSearchLength);
        RuleFor(request => request.SubjectCode).MaximumLength(50);
        RuleFor(request => request.Group).Must(HaveAtMostMaxValues).WithMessage(TooManyValuesMessage);
        RuleForEach(request => request.Group).MaximumLength(100);
        RuleFor(request => request.StartupIndustry).Must(HaveAtMostMaxValues).WithMessage(TooManyValuesMessage);
        RuleForEach(request => request.StartupIndustry).MaximumLength(100);
        RuleFor(request => request.Major).Must(HaveAtMostMaxValues).WithMessage(TooManyValuesMessage);
        RuleForEach(request => request.Major)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Must(ProjectDataQuery.IsMajorFilterValue)
            .WithMessage("Major must be a known major code or UNSPECIFIED.");
        RuleFor(request => request.Semester)
            .Must(value => ProjectDataQuery.ParseTerm(value) is not null)
            .When(request => !string.IsNullOrWhiteSpace(request.Semester))
            .WithMessage("Semester must be SP, SU or FA.");
        RuleFor(request => request.Year).InclusiveBetween(2000, 9999).When(request => request.Year.HasValue);
        RuleFor(request => request.LecturerId).Must(HaveAtMostMaxValues).WithMessage(TooManyValuesMessage);
        RuleForEach(request => request.LecturerId).NotEqual(Guid.Empty);
        RuleFor(request => request.MentorId).Must(HaveAtMostMaxValues).WithMessage(TooManyValuesMessage);
        RuleForEach(request => request.MentorId).NotEqual(Guid.Empty);
        RuleFor(request => request.Achievement).Must(HaveAtMostMaxValues).WithMessage(TooManyValuesMessage);
        RuleForEach(request => request.Achievement)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Must(value => ProjectAchievementMapping.Canonicalize(value) is not null)
            .WithMessage($"Achievement must be {ProjectAchievementNames.Description}.");
        RuleFor(request => request.Competition).Must(HaveAtMostMaxValues).WithMessage(TooManyValuesMessage);
        RuleForEach(request => request.Competition)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Must(value => ProjectCompetitionMapping.Canonicalize(value) is not null)
            .WithMessage("Competition must be one of the listed competitions.");
        RuleFor(request => request.CompetitionStage).Must(HaveAtMostMaxValues).WithMessage(TooManyValuesMessage);
        RuleForEach(request => request.CompetitionStage)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Must(value => ProjectCompetitionMapping.ParseStage(value) is not null)
            .WithMessage($"Competition stage must be {string.Join(", ", ProjectCompetitionStageNames.All)}.");
        RuleFor(request => request.CompetitionYear).Must(HaveAtMostMaxValues).WithMessage(TooManyValuesMessage);
        RuleForEach(request => request.CompetitionYear)
            .InclusiveBetween(ProjectCompetitionNames.MinYear, ProjectCompetitionNames.MaxYear);
        RuleFor(request => request.SortBy)
            .Must(value => ProjectDataQuery.AllowedSortFields.Contains(value!.Trim(), StringComparer.OrdinalIgnoreCase))
            .When(request => !string.IsNullOrWhiteSpace(request.SortBy))
            .WithMessage($"Sort field must be one of: {string.Join(", ", ProjectDataQuery.AllowedSortFields)}.");
    }
}
