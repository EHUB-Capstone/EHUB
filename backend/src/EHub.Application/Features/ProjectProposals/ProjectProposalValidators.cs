using EHub.Contracts.ProjectProposals;
using FluentValidation;

namespace EHub.Application.Features.ProjectProposals;

public abstract class ProjectProposalContentRequestValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : ProjectProposalContentRequest
{
    protected ProjectProposalContentRequestValidator()
    {
        RuleFor(request => request.Title).MaximumLength(200);
        RuleFor(request => request.StartupName).MaximumLength(150);
        RuleFor(request => request.Tagline).MaximumLength(200);
        RuleFor(request => request.Problem).MaximumLength(3_000);
        RuleFor(request => request.Solution).MaximumLength(3_000);
        RuleFor(request => request.TargetCustomers).MaximumLength(2_000);
        RuleFor(request => request.ValueProposition).MaximumLength(2_000);
        RuleFor(request => request.MarketSize).MaximumLength(2_500);
        RuleFor(request => request.Competitors).MaximumLength(3_000);
        RuleFor(request => request.BusinessModel).MaximumLength(3_000);
        RuleFor(request => request.RevenueModel).MaximumLength(2_000);
        RuleFor(request => request.MarketingStrategy).MaximumLength(3_000);
        RuleFor(request => request.Technology).MaximumLength(3_000);
        RuleFor(request => request.FinancialPlan).MaximumLength(3_000);
        RuleFor(request => request.Roadmap).MaximumLength(3_000);
        RuleFor(request => request.TeamIntroduction).MaximumLength(2_000);
        RuleFor(request => request.ChangeNote).MaximumLength(1_000);
        RuleFor(request => request)
            .Must(request => ProjectProposalValidationRules.TotalContentLength(request) <= ProjectProposalValidationRules.MaximumTotalContentLength)
            .WithMessage($"Proposal content must not exceed {ProjectProposalValidationRules.MaximumTotalContentLength} characters in total.");
    }
}

public sealed class CreateProjectProposalRequestValidator : ProjectProposalContentRequestValidator<CreateProjectProposalRequest>;

public sealed class UpdateProjectProposalRequestValidator : ProjectProposalContentRequestValidator<UpdateProjectProposalRequest>
{
    public UpdateProjectProposalRequestValidator()
    {
        RuleFor(request => request.RowVersion).Must(ProjectProposalValidationRules.IsValidRowVersion)
            .WithMessage("A valid rowVersion is required.");
    }
}

public sealed class SubmitProjectProposalRequestValidator : AbstractValidator<SubmitProjectProposalRequest>
{
    public SubmitProjectProposalRequestValidator()
    {
        RuleFor(request => request.RowVersion).Must(ProjectProposalValidationRules.IsValidRowVersion)
            .WithMessage("A valid rowVersion is required.");
        RuleFor(request => request.ChangeNote).MaximumLength(1_000);
    }
}

public sealed class RestoreProjectProposalVersionRequestValidator : AbstractValidator<RestoreProjectProposalVersionRequest>
{
    public RestoreProjectProposalVersionRequestValidator()
    {
        RuleFor(request => request.RowVersion).Must(ProjectProposalValidationRules.IsValidRowVersion)
            .WithMessage("A valid rowVersion is required.");
        RuleFor(request => request.ChangeNote).MaximumLength(1_000);
    }
}

public sealed class ReviewProjectProposalRequestValidator : AbstractValidator<ReviewProjectProposalRequest>
{
    private static readonly string[] Decisions = ["Approved", "NeedsRevision", "Rejected"];

    public ReviewProjectProposalRequestValidator()
    {
        RuleFor(request => request.RowVersion).Must(ProjectProposalValidationRules.IsValidRowVersion)
            .WithMessage("A valid rowVersion is required.");
        RuleFor(request => request.Decision)
            .Must(decision => Decisions.Contains(decision, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Decision must be Approved, NeedsRevision, or Rejected.");
        RuleFor(request => request.Feedback).NotEmpty().MinimumLength(3).MaximumLength(1_000);
    }
}

internal static class ProjectProposalValidationRules
{
    internal const int MaximumTotalContentLength = 30_000;

    internal static bool IsValidRowVersion(string value) => uint.TryParse(value, out _);

    internal static int TotalContentLength(ProjectProposalContentRequest request) =>
        ContentValues(request).Sum(value => value?.Trim().Length ?? 0);

    internal static IEnumerable<string?> ContentValues(ProjectProposalContentRequest request)
    {
        yield return request.Title;
        yield return request.StartupName;
        yield return request.Tagline;
        yield return request.Problem;
        yield return request.Solution;
        yield return request.TargetCustomers;
        yield return request.ValueProposition;
        yield return request.MarketSize;
        yield return request.Competitors;
        yield return request.BusinessModel;
        yield return request.RevenueModel;
        yield return request.MarketingStrategy;
        yield return request.Technology;
        yield return request.FinancialPlan;
        yield return request.Roadmap;
        yield return request.TeamIntroduction;
    }
}
