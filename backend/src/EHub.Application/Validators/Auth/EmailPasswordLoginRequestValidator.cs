using FluentValidation;
using EHub.Contracts.Auth;

namespace EHub.Application.Validators.Auth;

public sealed class EmailPasswordLoginRequestValidator : AbstractValidator<EmailPasswordLoginRequest>
{
    public EmailPasswordLoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not in a valid format.")
            .MaximumLength(320).WithMessage("Email must not exceed 320 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters.");
    }
}
