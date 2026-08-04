using EHub.Contracts.Subjects;
using FluentValidation;

namespace EHub.Application.Validators.Subjects;

public sealed class CreateSubjectRequestValidator : AbstractValidator<CreateSubjectRequest>
{
    public CreateSubjectRequestValidator()
    {
        RuleFor(x => x.SubjectCode).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.SubjectName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Status).Must(IsSubjectStatus).WithMessage("Status must be active or disabled.");
    }

    private static bool IsSubjectStatus(string status) =>
        status.Equals("active", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("disabled", StringComparison.OrdinalIgnoreCase);
}

public sealed class UpdateSubjectRequestValidator : AbstractValidator<UpdateSubjectRequest>
{
    public UpdateSubjectRequestValidator()
    {
        RuleFor(x => x.SubjectName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Status).Must(status =>
            status.Equals("active", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("disabled", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Status must be active or disabled.");
    }
}

public sealed class SetCurrentSemesterRequestValidator : AbstractValidator<SetCurrentSemesterRequest>
{
    public SetCurrentSemesterRequestValidator()
    {
        RuleFor(x => x.Semester).Must(value =>
            value.Equals("SP", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("SU", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("FA", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Semester must be SP, SU, or FA.");
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
    }
}

public sealed class SaveRoadmapItemRequestValidator : AbstractValidator<SaveRoadmapItemRequest>
{
    public SaveRoadmapItemRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CourseCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.WeekNumber).InclusiveBetween(1, 10);
        RuleFor(x => x.Priority).Must(value => new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" }.Contains(value.ToUpperInvariant()));
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0).When(x => x.EstimatedHours.HasValue);
        RuleForEach(x => x.Tags).MaximumLength(50);
    }
}

public sealed class SaveRubricRequestValidator : AbstractValidator<SaveRubricRequest>
{
    public SaveRubricRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CheckpointNumber).InclusiveBetween(1, 20).When(x => x.CheckpointNumber.HasValue);
        RuleFor(x => x.TotalWeight).InclusiveBetween(0, 100);
        RuleFor(x => x.Status).Must(value => new[] { "DRAFT", "ACTIVE", "ARCHIVED" }.Contains(value.ToUpperInvariant()));
    }
}

public sealed class SaveRubricCriterionRequestValidator : AbstractValidator<SaveRubricCriterionRequest>
{
    public SaveRubricCriterionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.MaxScore).GreaterThan(0);
        RuleFor(x => x.Weight).InclusiveBetween(0, 100);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
