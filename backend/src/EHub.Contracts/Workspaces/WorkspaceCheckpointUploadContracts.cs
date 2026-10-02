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
