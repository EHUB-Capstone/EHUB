using EHub.Contracts.Auth;
using FluentValidation;

namespace EHub.Application.Validators.Auth;

public sealed class VerifyRegistrationOtpRequestValidator : AbstractValidator<VerifyRegistrationOtpRequest>
{
    public VerifyRegistrationOtpRequestValidator()
    {
        RuleFor(request => request.RegistrationId)
            .NotEmpty().WithMessage("Registration ID is required.");

        RuleFor(request => request.Otp)
            .NotEmpty().WithMessage("Verification code is required.")
            .Matches("^[0-9]{6}$").WithMessage("Verification code must contain exactly 6 digits.");
    }
}
