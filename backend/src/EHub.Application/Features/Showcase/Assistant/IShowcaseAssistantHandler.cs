using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Assistant;

public interface IShowcaseAssistantHandler
{
    Task<Result<ShowcaseAssistantReplyDto>> AskAsync(
        ShowcaseAssistantChatRequest request, Guid userId, CancellationToken cancellationToken = default);
}
