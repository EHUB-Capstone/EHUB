using EHub.Contracts.Mentoring;
using FluentValidation;

namespace EHub.Application.Features.Mentoring.ManageProfiles;

public sealed class UpdateMentorProfileRequestValidator : AbstractValidator<UpdateMentorProfileRequest>
{
    public UpdateMentorProfileRequestValidator()
    {
        RuleFor(x => x.MentorType).Must(x => x is "Business" or "IT");
        RuleFor(x => x.Expertise).Must(x => ValidTags(x, true)).WithMessage("Provide 1–20 distinct expertise fields, each up to 80 characters.");
        RuleFor(x => x.StartupDomains).Must(x => ValidTags(x)).WithMessage("Startup domains must be distinct and valid.");
        RuleFor(x => x.TechnologySkills).Must(x => ValidTags(x)).WithMessage("Technology skills must be distinct and valid.");
        RuleFor(x => x.Tags).Must(x => ValidTags(x)).WithMessage("Mentor tags must be distinct and valid.");
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.Experience).MaximumLength(4000);
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.LinkedInUrl).Must(x => ValidUrl(x, 500)).WithMessage("LinkedIn must be a valid HTTPS URL.");
        RuleFor(x => x.PortfolioUrl).Must(x => ValidUrl(x, 1000)).WithMessage("Portfolio must be a valid HTTPS URL.");
        RuleFor(x => x.Experiences).NotNull().Must(x => x is not null && x.Count <= 40 &&
            x.All(e => e is not null) && x.Select(e => $"{e.Kind}:{e.Area?.Trim()}").Distinct(StringComparer.OrdinalIgnoreCase).Count() == x.Count)
            .WithMessage("Provide at most 40 distinct experience entries.");
        RuleForEach(x => x.Experiences).ChildRules(entry =>
        {
            entry.RuleFor(x => x.Kind).Must(x => x is "Startup" or "Technology");
            entry.RuleFor(x => x.Area).NotEmpty().MaximumLength(80).Must(x => !string.IsNullOrWhiteSpace(x));
            entry.RuleFor(x => x.Years).Must(x => x is null || x >= 0 && x <= 80 && decimal.Round(x.Value, 1) == x.Value)
                .WithMessage("Years must be between 0 and 80, with at most one decimal place.");
            entry.RuleFor(x => x.Level).MaximumLength(40);
            entry.RuleFor(x => x.Notes).MaximumLength(1000);
        });
    }

    private static bool ValidTags(string[]? values, bool required = false) => values is not null &&
        values.Length <= 20 && (!required || values.Length > 0) &&
        values.All(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length <= 80) &&
        values.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Length;
    private static bool ValidUrl(string? value, int max) => string.IsNullOrWhiteSpace(value) ||
        value.Length <= max && Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps;
}

public sealed class SaveAdminMentorProfileRequestValidator : AbstractValidator<SaveAdminMentorProfileRequest>
{
    public SaveAdminMentorProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100).Must(x => !string.IsNullOrWhiteSpace(x));
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.TemporaryPassword).MinimumLength(6).MaximumLength(100).When(x => x.TemporaryPassword is not null);
        RuleFor(x => x.Profile).NotNull().SetValidator(new UpdateMentorProfileRequestValidator());
        RuleFor(x => x.Profile.Bio).NotEmpty().When(x => x.Profile is not null);
    }
}
