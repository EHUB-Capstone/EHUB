using EHub.Contracts.Workspaces;
using FluentValidation;

namespace EHub.Application.Features.Workspaces.CheckpointLinks;

public sealed class SaveWorkspaceCheckpointLinkRequestValidator : AbstractValidator<SaveWorkspaceCheckpointLinkRequest>
{
    public SaveWorkspaceCheckpointLinkRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(request => request.Url)
            .NotEmpty()
            .MaximumLength(1_000)
            .Must(value => CheckpointLinkUrl.TryNormalize(value, out _))
            .WithMessage("URL must be a public HTTPS address without embedded credentials.");
    }
}
