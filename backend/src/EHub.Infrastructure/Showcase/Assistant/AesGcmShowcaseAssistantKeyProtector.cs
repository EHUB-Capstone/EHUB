using System.Security.Cryptography;
using System.Text;
using EHub.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace EHub.Infrastructure.Showcase.Assistant;

/// <summary>
/// AES-256-GCM with a fresh random nonce for every key it protects. The owner's id is authenticated together with the data, so a
/// stored blob copied to another user's row, or altered in any way, no longer opens. The blob starts with a format byte and the
/// id of the encryption key used, which lets the active key be replaced without losing what was stored earlier.
/// </summary>
public sealed class AesGcmShowcaseAssistantKeyProtector : IShowcaseAssistantKeyProtector
{
    private const byte FormatVersion = 1;
    private const int HeaderSize = 2;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    // A Gemini key is about forty characters. Anything much larger in the database is not something this class made.
    private const int MaximumBlobSize = 1024;
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("ehub:showcase-assistant-key:v1:");

    private readonly Dictionary<byte, byte[]> keys;
    private readonly byte activeKeyId;

    public AesGcmShowcaseAssistantKeyProtector(IOptions<ShowcaseAssistantKeyEncryptionOptions> options)
    {
        var settings = options.Value;
        if (!settings.IsEmpty && settings.TryParse(out var parsed, out var active))
        {
            keys = parsed;
            activeKeyId = active;
            IsAvailable = true;
        }
        else keys = [];
    }

    public bool IsAvailable { get; }

    public byte[] Protect(Guid userId, string apiKey)
    {
        if (!IsAvailable) throw new InvalidOperationException("No encryption key is configured for stored Showcase assistant keys.");
        var plaintext = Encoding.UTF8.GetBytes(apiKey);
        try
        {
            var blob = new byte[HeaderSize + NonceSize + plaintext.Length + TagSize];
            blob[0] = FormatVersion;
            blob[1] = activeKeyId;
            var nonce = blob.AsSpan(HeaderSize, NonceSize);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(keys[activeKeyId], TagSize);
            aes.Encrypt(nonce, plaintext, blob.AsSpan(HeaderSize + NonceSize, plaintext.Length),
                blob.AsSpan(HeaderSize + NonceSize + plaintext.Length, TagSize), AssociatedData(userId));
            return blob;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public bool TryUnprotect(Guid userId, byte[] protectedKey, out string apiKey)
    {
        apiKey = string.Empty;
        var plaintext = Open(userId, protectedKey);
        if (plaintext is null) return false;
        try
        {
            apiKey = Encoding.UTF8.GetString(plaintext);
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public bool CanOpen(Guid userId, byte[] protectedKey)
    {
        var plaintext = Open(userId, protectedKey);
        if (plaintext is null) return false;
        CryptographicOperations.ZeroMemory(plaintext);
        return true;
    }

    /// <summary>The decrypted bytes, or null when the blob is not usable. The caller clears the bytes when done.</summary>
    private byte[]? Open(Guid userId, byte[] protectedKey)
    {
        if (!TryReadHeader(protectedKey, out var key)) return null;
        var cipherLength = protectedKey.Length - HeaderSize - NonceSize - TagSize;
        var plaintext = new byte[cipherLength];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(protectedKey.AsSpan(HeaderSize, NonceSize), protectedKey.AsSpan(HeaderSize + NonceSize, cipherLength),
                protectedKey.AsSpan(HeaderSize + NonceSize + cipherLength, TagSize), plaintext, AssociatedData(userId));
            return plaintext;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            return null;
        }
    }

    private bool TryReadHeader(byte[] blob, out byte[] key)
    {
        key = [];
        return IsAvailable && blob.Length is > HeaderSize + NonceSize + TagSize and <= MaximumBlobSize && blob[0] == FormatVersion
            && keys.TryGetValue(blob[1], out key!);
    }

    private static byte[] AssociatedData(Guid userId)
    {
        var data = new byte[Purpose.Length + 16];
        Purpose.CopyTo(data, 0);
        userId.TryWriteBytes(data.AsSpan(Purpose.Length));
        return data;
    }
}
