using System.Net.Mail;
using EHub.Contracts.Mentors;
using EHub.Domain.Enums;
using FluentValidation;

namespace EHub.Application.Features.Admin.MentorProfiles;

public sealed class UpdateMentorProfileRequestValidator : AbstractValidator<UpdateMentorProfileRequest>
{
    public UpdateMentorProfileRequestValidator()
    {
        RuleFor(x => x.RowVersion).Must(value => uint.TryParse(value, out _)).WithMessage("A valid rowVersion is required.");
        RuleFor(x => x.Status).Must(value => Enum.TryParse<MentorProfileStatus>(value, ignoreCase: true, out var status) && Enum.IsDefined(status))
            .WithMessage("Status must be Active, Inactive or Unavailable.");
        RuleFor(x => x.Expertise).Custom((value, context) =>
        {
            var (_, error) = MentorProfileRules.NormalizeExpertise(value);
            if (error is not null) context.AddFailure(nameof(UpdateMentorProfileRequest.Expertise), error);
        });
        RuleFor(x => x.Bio).Must(value => MentorProfileRules.ValidateBio(value) is null)
            .WithMessage($"Background may contain at most {MentorProfileRules.MaximumBioLength} characters.");
        RuleFor(x => x.AvailabilityNote).Must(value => MentorProfileRules.ValidateAvailabilityNote(value) is null)
            .WithMessage($"Availability note may contain at most {MentorProfileRules.MaximumAvailabilityNoteLength} characters.");
        RuleFor(x => x.Organization).MaximumLength(200);
        RuleFor(x => x.Department).MaximumLength(200);
        RuleFor(x => x.JobTitle).MaximumLength(200);
        RuleFor(x => x.ContractType).MaximumLength(100);
        RuleFor(x => x.EducationLevel).MaximumLength(200);
        RuleFor(x => x.CurrentAddress).MaximumLength(500);
        RuleFor(x => x.LinkedInUrl).MaximumLength(500)
            .Must(value => string.IsNullOrWhiteSpace(value) ||
                (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            .WithMessage("LinkedIn URL must be a valid http or https address.");
        RuleFor(x => x.FptEmail).MaximumLength(320)
            .Must(value => string.IsNullOrWhiteSpace(value) || (MailAddress.TryCreate(value.Trim(), out var address) && address.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage("FPT email must be a valid email address.");
        RuleFor(x => x.DateOfBirth)
            .Must(value => value is null || (value.Value.Year >= 1900 && value.Value <= DateOnly.FromDateTime(DateTime.UtcNow)))
            .WithMessage("Date of birth is outside the allowed range.");
    }
}
