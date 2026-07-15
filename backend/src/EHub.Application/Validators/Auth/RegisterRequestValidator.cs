using FluentValidation;
using EHub.Contracts.Auth;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using System.Linq;

namespace EHub.Application.Validators.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Full name must not consist of only whitespace.")
            .MinimumLength(2).WithMessage("Full name must be at least 2 characters.")
            .MaximumLength(150).WithMessage("Full name must not exceed 150 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not in a valid format.")
            .MaximumLength(320).WithMessage("Email must not exceed 320 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(x => x.Password).WithMessage("Confirm password must match the password.")
            .WithErrorCode(ErrorCodes.AuthPasswordConfirmationMismatch);

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => SystemRoles.PublicRegisterRoles.Contains(role))
            .WithMessage($"Role is invalid. Only {string.Join(", ", SystemRoles.PublicRegisterRoles)} roles are allowed for public registration.")
            .WithErrorCode(ErrorCodes.AuthInvalidRole);

        RuleFor(x => x.MajorCode)
            .NotEmpty().WithMessage("Major is required for Student role.")
            .WithErrorCode(ErrorCodes.AuthStudentMajorRequired)
            .When(x => x.Role == SystemRoles.Student);

        RuleFor(x => x.MajorCode)
            .Must(major => MajorCodes.IsValid(major)).WithMessage("Selected major is invalid.")
            .WithErrorCode(ErrorCodes.AuthInvalidMajor)
            .When(x => !string.IsNullOrEmpty(x.MajorCode));
    }
}
