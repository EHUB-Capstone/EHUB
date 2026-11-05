using EHub.Contracts.Showcase;
using EHub.Domain.Enums;
using FluentValidation;

namespace EHub.Application.Features.Showcase.Manage;

/// <summary>Structure of the showcase-only content; project name, description and the like live in the Project Profile.</summary>
public sealed class SaveShowcaseRequestValidator : AbstractValidator<SaveShowcaseRequest>
{
    public const int MaximumLinks = 8;

    public SaveShowcaseRequestValidator()
    {
        RuleFor(request => request.Highlights).MaximumLength(2000);

        RuleFor(request => request.DevelopmentStage)
            .Must(value => Enum.TryParse<ShowcaseDevelopmentStage>(value, true, out _))
            .WithMessage("Development stage must be Idea, Mvp, Beta or Launched.");

        RuleFor(request => request.Links)
            .Must(links => links is not null && links.Count <= MaximumLinks)
            .WithMessage($"A showcase can have at most {MaximumLinks} links.");

        RuleForEach(request => request.Links).ChildRules(link =>
        {
            link.RuleFor(item => item.Label).NotEmpty().MaximumLength(100);
            link.RuleFor(item => item.LinkType)
                .Must(value => Enum.TryParse<ShowcaseLinkType>(value, true, out _))
                .WithMessage("Link type is invalid.");
            link.RuleFor(item => item.Url)
                .MaximumLength(1000)
                .Must(IsPublicWebUrl)
                .WithMessage("Link must be a valid http or https URL.");
            // A Video link is previewed inside the public feed, so only a YouTube or Vimeo https link is accepted.
            link.RuleFor(item => item.Url)
                .Must((item, url) => !IsVideoType(item.LinkType) || ShowcaseVideoLinks.IsSupported(url))
                .WithMessage("A Video link must be an https YouTube or Vimeo link.");
        });
    }

    private static bool IsVideoType(string? value) =>
        Enum.TryParse<ShowcaseLinkType>(value, true, out var type) && type == ShowcaseLinkType.Video;

    /// <summary>Only plain http(s) URLs without embedded credentials are accepted (blocks javascript:, data:, etc.).</summary>
    internal static bool IsPublicWebUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        !string.IsNullOrEmpty(uri.Host);
}
