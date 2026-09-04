using EHub.Contracts.Workspaces;
using FluentValidation;

namespace EHub.Application.Features.Workspaces;

public sealed class SaveWeeklyTaskRequestValidator : AbstractValidator<SaveWeeklyTaskRequest>
{
    public SaveWeeklyTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(4_000);
        RuleFor(x => x.WeekNumber).InclusiveBetween(1, 10);
        RuleFor(x => x.TaskType).Must(value => new[] { "COURSE_TEMPLATE", "CLASS_TASK", "TEAM_TASK" }.Contains(value?.ToUpperInvariant())).WithMessage("Task type is invalid.");
        RuleFor(x => x.Priority).Must(value => new[] { "LOW", "MEDIUM", "HIGH", "CRITICAL" }.Contains(value?.ToUpperInvariant())).WithMessage("Priority is invalid.");
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.StartDate.HasValue && x.DueDate.HasValue);
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0).When(x => x.EstimatedHours.HasValue);
    }
}

public sealed class SaveShortcutRequestValidator : AbstractValidator<SaveShortcutRequest>
{
    public SaveShortcutRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(1_000).Must(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)).WithMessage("URL must be a valid HTTP or HTTPS address.");
        RuleFor(x => x.Description).MaximumLength(1_000);
    }
}
