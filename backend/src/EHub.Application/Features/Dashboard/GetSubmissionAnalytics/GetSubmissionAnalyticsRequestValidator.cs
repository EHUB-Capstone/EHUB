using EHub.Contracts.Dashboard;
using FluentValidation;

namespace EHub.Application.Features.Dashboard.GetSubmissionAnalytics;

public sealed class GetSubmissionAnalyticsRequestValidator : AbstractValidator<GetSubmissionAnalyticsRequest>
{
    public GetSubmissionAnalyticsRequestValidator()
    {
        RuleFor(request => request.Semester)
            .Must(value => value is null || new[] { "SP", "SU", "FA" }.Contains(value.Trim().ToUpperInvariant()))
            .WithMessage("Semester must be SP, SU, or FA.");
        RuleFor(request => request.Year).InclusiveBetween(2000, 9999).When(request => request.Year.HasValue);
        RuleFor(request => request.ClassId).NotEqual(Guid.Empty).When(request => request.ClassId.HasValue);
        RuleFor(request => request.TeamId).NotEqual(Guid.Empty).When(request => request.TeamId.HasValue);
        RuleFor(request => request.CheckpointNumber).GreaterThan(0).When(request => request.CheckpointNumber.HasValue);
    }
}
