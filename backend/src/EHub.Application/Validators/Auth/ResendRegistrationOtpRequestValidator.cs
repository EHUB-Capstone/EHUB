using EHub.Contracts.Auth;
using FluentValidation;

namespace EHub.Application.Validators.Auth;

public sealed class ResendRegistrationOtpRequestValidator : AbstractValidator<ResendRegistrationOtpRequest>
{
    public ResendRegistrationOtpRequestValidator()
    {
        RuleFor(request => request.RegistrationId)
            .NotEmpty().WithMessage("Registration ID is required.");
    }
}
