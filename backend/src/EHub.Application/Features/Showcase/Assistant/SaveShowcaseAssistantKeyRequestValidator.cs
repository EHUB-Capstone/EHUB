using EHub.Contracts.Showcase;
using FluentValidation;

namespace EHub.Application.Features.Showcase.Assistant;

/// <summary>Checks the shape of the key only (the provider decides whether it works). No message ever repeats what was sent.</summary>
public sealed class SaveShowcaseAssistantKeyRequestValidator : AbstractValidator<SaveShowcaseAssistantKeyRequest>
{
    public const string Message = "API key không đúng định dạng. Hãy dán lại đúng key từ Google AI Studio.";

    public SaveShowcaseAssistantKeyRequestValidator()
    {
        RuleFor(request => request.ApiKey)
            .Must(key => ShowcaseAssistantApiKey.IsWellFormed(key?.Trim()))
            .WithMessage(Message);
    }
}
