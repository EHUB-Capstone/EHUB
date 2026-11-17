using EHub.Contracts.Showcase;
using FluentValidation;

namespace EHub.Application.Features.Showcase.Assistant;

public sealed class ShowcaseAssistantChatRequestValidator : AbstractValidator<ShowcaseAssistantChatRequest>
{
    public const int MaximumMessageLength = 500;
    public const int MaximumHistoryTurns = 12;
    public const int MaximumHistoryContentLength = 1500;

    public ShowcaseAssistantChatRequestValidator()
    {
        RuleFor(request => request.Message)
            .Must(message => !string.IsNullOrWhiteSpace(message)).WithMessage("Hãy nhập câu hỏi.")
            .Must(message => message is null || message.Trim().Length <= MaximumMessageLength)
            .WithMessage($"Câu hỏi tối đa {MaximumMessageLength} ký tự.");
        RuleFor(request => request.History)
            .NotNull().WithMessage("Lịch sử hội thoại không hợp lệ.")
            .Must(history => history is null || history.Count <= MaximumHistoryTurns)
            .WithMessage("Lịch sử hội thoại quá dài.");
        RuleForEach(request => request.History).ChildRules(turn =>
        {
            turn.RuleFor(item => item.Role)
                .Must(role => string.Equals(role, "user", StringComparison.Ordinal) || string.Equals(role, "assistant", StringComparison.Ordinal))
                .WithMessage("Vai trò trong lịch sử hội thoại không hợp lệ.");
            turn.RuleFor(item => item.Content)
                .Must(content => !string.IsNullOrWhiteSpace(content) && content.Length <= MaximumHistoryContentLength)
                .WithMessage("Nội dung trong lịch sử hội thoại không hợp lệ.");
        }).When(request => request.History is not null);
    }
}
