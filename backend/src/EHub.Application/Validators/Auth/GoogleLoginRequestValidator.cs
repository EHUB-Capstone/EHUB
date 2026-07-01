using FluentValidation;
using EHub.Contracts.Auth;

namespace EHub.Application.Validators.Auth;

public sealed class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("Google ID Token is required.")
            .MaximumLength(5000).WithMessage("Google ID Token must not exceed 5000 characters.");
    }
}
