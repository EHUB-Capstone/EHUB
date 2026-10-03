using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.ProjectData;
using FluentValidation;

namespace EHub.Application.Features.ProjectData.GetProjectData;

public sealed class GetProjectDataRequestValidator : AbstractValidator<GetProjectDataRequest>
{
    public GetProjectDataRequestValidator()
    {
        RuleFor(request => request.PageIndex).GreaterThanOrEqualTo(1);
        RuleFor(request => request.PageSize)
            .Must(size => ProjectDataQuery.AllowedPageSizes.Contains(size))
            .WithMessage($"Page size must be one of: {string.Join(", ", ProjectDataQuery.AllowedPageSizes)}.");
        RuleFor(request => request.Search).MaximumLength(ProjectDataQuery.MaxSearchLength);
        RuleFor(request => request.SubjectCode).MaximumLength(50);
        RuleFor(request => request.Group).MaximumLength(100);
        RuleFor(request => request.StartupIndustry).MaximumLength(100);
        RuleFor(request => request.Semester)
            .Must(value => ProjectDataQuery.ParseTerm(value) is not null)
            .When(request => !string.IsNullOrWhiteSpace(request.Semester))
            .WithMessage("Semester must be SP, SU or FA.");
        RuleFor(request => request.Year).InclusiveBetween(2000, 9999).When(request => request.Year.HasValue);
        RuleFor(request => request.LecturerId).NotEqual(Guid.Empty).When(request => request.LecturerId.HasValue);
        RuleFor(request => request.MentorId).NotEqual(Guid.Empty).When(request => request.MentorId.HasValue);
        RuleFor(request => request.Achievement)
            .Must(value => ProjectAchievementMapping.Canonicalize(value) is not null)
            .When(request => !string.IsNullOrWhiteSpace(request.Achievement))
            .WithMessage("Achievement must be Potential, Funded or Awarded.");
        RuleFor(request => request.SortBy)
            .Must(value => ProjectDataQuery.AllowedSortFields.Contains(value!.Trim(), StringComparer.OrdinalIgnoreCase))
            .When(request => !string.IsNullOrWhiteSpace(request.SortBy))
            .WithMessage($"Sort field must be one of: {string.Join(", ", ProjectDataQuery.AllowedSortFields)}.");
    }
}
