using FluentValidation;
using EHub.Contracts.Auth;

namespace EHub.Application.Validators.Auth;

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
