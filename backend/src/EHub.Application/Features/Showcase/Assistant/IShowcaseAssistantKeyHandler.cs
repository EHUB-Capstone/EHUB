using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Assistant;

/// <summary>
/// The Gemini key a user chose to keep on the server. Everything here acts on the signed-in user's own key only: there is no way
/// to name another user, and no operation ever returns the key.
/// </summary>
public interface IShowcaseAssistantKeyHandler
{
    Task<Result<ShowcaseAssistantKeyStatusDto>> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Stores the key encrypted, replacing any key stored before.</summary>
    Task<Result<ShowcaseAssistantKeyStatusDto>> SaveAsync(
        SaveShowcaseAssistantKeyRequest request, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Removes the stored key. Removing a key that is not there succeeds too.</summary>
    Task<Result<ShowcaseAssistantKeyStatusDto>> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gives the assistant the key its owner stored. Only the chat use case takes this; it is deliberately not part of
/// <see cref="IShowcaseAssistantKeyHandler"/>, which the API exposes, so no endpoint can ever hand a key back.
/// </summary>
public interface IStoredShowcaseAssistantKey
{
    /// <summary>The user's stored key, or null when there is none or it can no longer be opened.</summary>
    Task<string?> RevealAsync(Guid userId, CancellationToken cancellationToken = default);
}
