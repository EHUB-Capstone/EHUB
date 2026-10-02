namespace EHub.Infrastructure.Options;

/// <summary>Cloudflare R2 (S3-compatible) credentials. Values come from User Secrets or environment variables only.</summary>
public sealed class R2Options
{
    public const string SectionName = "R2";

    public string AccountId { get; init; } = string.Empty;
    public string AccessKeyId { get; init; } = string.Empty;
    public string SecretAccessKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
}
