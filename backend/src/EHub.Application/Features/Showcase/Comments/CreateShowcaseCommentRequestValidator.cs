using EHub.Contracts.Showcase;
using FluentValidation;

namespace EHub.Application.Features.Showcase.Comments;

public sealed class CreateShowcaseCommentRequestValidator : AbstractValidator<CreateShowcaseCommentRequest>
{
    public CreateShowcaseCommentRequestValidator()
    {
        RuleFor(item => item.Content).NotEmpty().MaximumLength(2_000);
    }
}
