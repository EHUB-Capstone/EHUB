using EHub.Contracts.Workspaces;
using FluentValidation;

namespace EHub.Application.Features.Workspaces.DeadlineExtensions;

public sealed class CreateCheckpointDeadlineExtensionRequestValidator : AbstractValidator<CreateCheckpointDeadlineExtensionRequest>
{
    public CreateCheckpointDeadlineExtensionRequestValidator()
    {
        RuleFor(item => item.Reason)
            .NotEmpty().WithMessage("A reason is required.")
            .MaximumLength(2000).WithMessage("The reason must not exceed 2000 characters.");
    }
}
