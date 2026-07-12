using FluentValidation;
using EHub.Contracts.Auth;
using EHub.Shared.Constants;
using System.Linq;

namespace EHub.Application.Validators.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MinimumLength(2).WithMessage("Full name must be at least 2 characters.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not in a valid format.")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(x => x.Password).WithMessage("Confirm password must match the password.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => SystemRoles.PublicRegisterRoles.Contains(role))
            .WithMessage($"Role is invalid. Only {string.Join(", ", SystemRoles.PublicRegisterRoles)} roles are allowed for public registration.");

        RuleFor(x => x.Major)
            .NotEmpty().WithMessage("Major is required for Student role.")
            .When(x => x.Role == SystemRoles.Student);

        RuleFor(x => x.Major)
            .MaximumLength(100).WithMessage("Major must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Major));
    }
}
