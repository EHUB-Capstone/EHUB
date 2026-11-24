namespace EHub.Contracts.Showcase;

/// <summary>What the server knows about the Gemini key the current user may have stored. It never carries the key itself.</summary>
public sealed class ShowcaseAssistantKeyStatusDto
{
    /// <summary>
    /// False when the server has no encryption key configured. A key cannot be stored then; the user can still keep it in
    /// their browser.
    /// </summary>
    public bool StorageAvailable { get; init; }

    /// <summary>A stored key exists and can be opened, so asking the assistant needs no key from the browser.</summary>
    public bool HasKey { get; init; }

    /// <summary>The last four characters of the stored key, so its owner can tell which key it is. Null when none is stored.</summary>
    public string? Last4 { get; init; }

    public DateTime? UpdatedAt { get; init; }
}

public sealed class SaveShowcaseAssistantKeyRequest
{
    /// <summary>The user's own Gemini API key. It is encrypted before it is stored and is never returned or logged.</summary>
    public string ApiKey { get; init; } = string.Empty;
}
