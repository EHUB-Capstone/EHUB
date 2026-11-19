using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Assistant;

public interface IShowcaseAssistantHandler
{
    /// <param name="apiKey">The caller's own Gemini key from the request header. The server has none of its own; it is used for this call only.</param>
    Task<Result<ShowcaseAssistantReplyDto>> AskAsync(
        ShowcaseAssistantChatRequest request, string? apiKey, Guid userId, CancellationToken cancellationToken = default);
}
