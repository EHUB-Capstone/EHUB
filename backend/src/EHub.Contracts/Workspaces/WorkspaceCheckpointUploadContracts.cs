namespace EHub.Contracts.Workspaces;

public sealed class InitiateCheckpointFileUploadRequest
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long Size { get; init; }
}

public sealed class CheckpointFileUploadSessionResponse
{
    public Guid UploadId { get; init; }
    public string UploadUrl { get; init; } = string.Empty;
    public string Method { get; init; } = "PUT";
    /// <summary>Headers the browser must send unchanged with the PUT request.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public DateTime UrlExpiresAt { get; init; }
    public DateTime SessionExpiresAt { get; init; }
    public long MaxFileSize { get; init; }
}

public sealed class CheckpointFileDownloadUrlResponse
{
    public string Url { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

/// <summary>Answer of <c>preview-source</c>: where the browser can read the PDF preview, or why it cannot yet.</summary>
public sealed class CheckpointFilePreviewSourceResponse
{
    /// <summary>
    /// <c>Ready</c> (read the PDF from <see cref="Url"/>), <c>Preparing</c> (the PDF is being generated; ask again),
    /// <c>Proxy</c> (use the <c>preview</c> endpoint, e.g. files still on Cloudinary), <c>Failed</c>,
    /// <c>TooLarge</c> or <c>Unsupported</c> (see <see cref="Message"/>).
    /// </summary>
    public string Status { get; init; } = string.Empty;
    public string? Url { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? Message { get; init; }
    public int? RetryAfterSeconds { get; init; }
}
