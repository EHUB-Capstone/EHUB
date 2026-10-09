namespace EHub.Shared.Constants;

public static class SubmissionFileLimits
{
    public const long MaxFileSizeBytes = 100L * 1024 * 1024;

    /// <summary>How long the presigned PUT URL stays valid. Only the start of the request is checked by R2.</summary>
    public static readonly TimeSpan PresignedUploadLifetime = TimeSpan.FromMinutes(10);

    /// <summary>How long a started upload may take before the browser must call complete.</summary>
    public static readonly TimeSpan UploadSessionLifetime = TimeSpan.FromMinutes(60);

    public const int MaxPendingUploadSessionsPerUserTeam = 5;

    /// <summary>Background preview generation: attempts before a file is marked Failed.</summary>
    public const int MaxPreviewGenerationAttempts = 3;

    /// <summary>Wait before retry number 1, 2, ... (the last entry is reused if there are more attempts).</summary>
    public static readonly TimeSpan[] PreviewGenerationRetryDelays = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)];

    /// <summary>A claimed file is not picked up again for this long (covers a crash mid-conversion).</summary>
    public static readonly TimeSpan PreviewGenerationLease = TimeSpan.FromMinutes(5);

    /// <summary>An abandoned session is cleaned up this long after it expired.</summary>
    public static readonly TimeSpan UploadCleanupGrace = TimeSpan.FromMinutes(10);

    /// <summary>Finished upload sessions are deleted after this long.</summary>
    public static readonly TimeSpan UploadSessionRetention = TimeSpan.FromDays(7);

    /// <summary>R2 files larger than this are downloaded straight from R2 through a presigned GET URL.</summary>
    public const long DirectDownloadThresholdBytes = 10L * 1024 * 1024;

    public static readonly TimeSpan PresignedDownloadLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Presigned GET used by PDF.js to read a preview PDF straight from R2 (several Range requests).</summary>
    public static readonly TimeSpan PresignedPreviewLifetime = TimeSpan.FromMinutes(15);

    /// <summary>DOCX/PPTX larger than this are not converted to a PDF preview (RAM and LibreOffice timeout).</summary>
    public const long MaxPreviewConvertSizeBytes = 30L * 1024 * 1024;
}
