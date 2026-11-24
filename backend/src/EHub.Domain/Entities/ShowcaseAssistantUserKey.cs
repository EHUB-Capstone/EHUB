namespace EHub.Domain.Entities;

/// <summary>
/// The Gemini API key a user asked the server to keep for their own Showcase assistant questions. One row per user. The key
/// itself is never stored: only an authenticated encryption of it, bound to this user, so a copy of the database or of a backup
/// is useless without the server's encryption key.
/// </summary>
public sealed class ShowcaseAssistantUserKey
{
    public Guid UserId { get; set; }

    /// <summary>Versioned AES-GCM blob (format, encryption key id, nonce, cipher text, tag). Opaque to everything but the protector.</summary>
    public byte[] Ciphertext { get; set; } = [];

    /// <summary>
    /// The last four characters of the key, so the owner can recognise which key is stored. Four characters of a roughly
    /// forty-character key do not make the key guessable.
    /// </summary>
    public string Last4 { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // PostgreSQL optimistic concurrency token mapped to the system xmin column.
    public uint Version { get; set; }
}
