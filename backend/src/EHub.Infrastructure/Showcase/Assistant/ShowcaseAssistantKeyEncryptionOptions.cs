using System.Globalization;

namespace EHub.Infrastructure.Showcase.Assistant;

/// <summary>
/// The server-side encryption keys that protect the Gemini keys users chose to store. Leave the section empty to switch storing
/// off: users can still keep their key in the browser. Each key is 32 random bytes in Base64; several can be configured so the
/// active one can be replaced while keys stored under an older one still open.
/// </summary>
public sealed class ShowcaseAssistantKeyEncryptionOptions
{
    public const string SectionName = "Showcase:Assistant:KeyEncryption";
    public const int KeySizeBytes = 32;

    /// <summary>Id (1 to 255) of the key used for everything stored from now on.</summary>
    public string ActiveKeyId { get; set; } = string.Empty;

    /// <summary>Key id (1 to 255) to Base64 key. An empty value is ignored, so an unused slot may be left blank.</summary>
    public Dictionary<string, string> Keys { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Nothing is configured at all, which means storing keys on the server is off rather than broken.</summary>
    internal bool IsEmpty => string.IsNullOrWhiteSpace(ActiveKeyId) && Keys.Values.All(string.IsNullOrWhiteSpace);

    /// <summary>Off, or fully and correctly configured. A half-configured or malformed section must stop the API at start.</summary>
    internal bool IsValid => IsEmpty || TryParse(out _, out _);

    internal bool TryParse(out Dictionary<byte, byte[]> keys, out byte activeId)
    {
        keys = [];
        activeId = 0;
        if (!TryParseId(ActiveKeyId, out activeId)) return false;
        foreach (var (name, value) in Keys)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (!TryParseId(name, out var id) || keys.ContainsKey(id)) return false;
            var bytes = new byte[KeySizeBytes];
            // A key of the wrong size, not valid Base64 or all zero (a placeholder) is a mistake, never a usable key.
            if (!Convert.TryFromBase64String(value.Trim(), bytes, out var written) || written != KeySizeBytes || bytes.All(item => item == 0))
                return false;
            keys[id] = bytes;
        }
        return keys.ContainsKey(activeId);
    }

    private static bool TryParseId(string? text, out byte id)
    {
        id = 0;
        if (!int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 255) return false;
        id = (byte)number;
        return true;
    }
}
