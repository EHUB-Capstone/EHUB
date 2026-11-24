namespace EHub.Application.Common.Interfaces.Services;

/// <summary>
/// Encrypts the Gemini key a user chose to keep on the server. The result is bound to its owner, so it cannot be opened for
/// anyone else, and it is useless without the encryption key held by the server (never in the database or in a backup of it).
/// </summary>
public interface IShowcaseAssistantKeyProtector
{
    /// <summary>
    /// False when the server has no encryption key configured. Nothing may be stored then, and a key is never stored unencrypted.
    /// </summary>
    bool IsAvailable { get; }

    /// <exception cref="InvalidOperationException">No encryption key is configured.</exception>
    byte[] Protect(Guid userId, string apiKey);

    /// <summary>
    /// False when the blob was made with an encryption key that is no longer configured, was altered, or belongs to another user.
    /// </summary>
    bool TryUnprotect(Guid userId, byte[] protectedKey, out string apiKey);

    /// <summary>
    /// Whether <see cref="TryUnprotect"/> would succeed for this user, so a status check and the chat always agree. The key is
    /// verified but never turned into a string.
    /// </summary>
    bool CanOpen(Guid userId, byte[] protectedKey);
}
