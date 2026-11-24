using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Assistant;

public interface IShowcaseAssistantHandler
{
    /// <param name="apiKey">
    /// The caller's own Gemini key from the request header, used for this call only. When it is empty, the key the caller stored on
    /// the server is used instead. The server has no key of its own.
    /// </param>
    Task<Result<ShowcaseAssistantReplyDto>> AskAsync(
        ShowcaseAssistantChatRequest request, string? apiKey, Guid userId, CancellationToken cancellationToken = default);
}
