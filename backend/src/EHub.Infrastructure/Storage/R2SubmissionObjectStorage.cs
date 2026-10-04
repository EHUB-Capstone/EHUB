using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Infrastructure.Options;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EHub.Infrastructure.Storage;

public sealed class R2SubmissionObjectStorage : ISubmissionObjectStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucketName;
    private readonly ILogger<R2SubmissionObjectStorage> _logger;

    public R2SubmissionObjectStorage(IOptions<R2Options> options, ILogger<R2SubmissionObjectStorage> logger)
    {
        var settings = options.Value;
        _bucketName = settings.BucketName;
        _logger = logger;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = $"https://{settings.AccountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                AuthenticationRegion = "auto",
                // R2 does not accept the SDK's default CRC32 trailer/checksum parameters on presigned requests.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            });
    }

    public PresignedObjectUpload CreatePresignedUpload(string objectKey, string contentType, long contentLength, TimeSpan lifetime)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(lifetime),
            ContentType = contentType
        };
        request.Headers["Content-Length"] = contentLength.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url = _client.GetPreSignedURL(request);
        return new PresignedObjectUpload(url, new Dictionary<string, string> { ["Content-Type"] = contentType });
    }

    public async Task<Result<StoredObjectInfo>> GetObjectInfoAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = await _client.GetObjectMetadataAsync(_bucketName, objectKey, cancellationToken);
            return Result.Success(new StoredObjectInfo(metadata.ContentLength, metadata.Headers.ContentType));
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Failure<StoredObjectInfo>(ErrorCodes.CommonNotFoundError, "The stored object was not found.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "R2 metadata lookup failed for object {ObjectKey}.", objectKey);
            return Result.Failure<StoredObjectInfo>(ErrorCodes.CommonUnexpectedError, "File storage is temporarily unavailable.");
        }
    }

    public async Task<Result<byte[]>> ReadRangeAsync(string objectKey, long offset, int length, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                ByteRange = new ByteRange(offset, offset + length - 1)
            }, cancellationToken);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return Result.Success(buffer.ToArray());
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Failure<byte[]>(ErrorCodes.CommonNotFoundError, "The stored object was not found.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "R2 range read failed for object {ObjectKey}.", objectKey);
            return Result.Failure<byte[]>(ErrorCodes.CommonUnexpectedError, "File storage is temporarily unavailable.");
        }
    }

    public async Task<Result<byte[]>> DownloadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _client.GetObjectAsync(_bucketName, objectKey, cancellationToken);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return Result.Success(buffer.ToArray());
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Failure<byte[]>(ErrorCodes.CommonNotFoundError, "The stored file was not found.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "R2 download failed for object {ObjectKey}.", objectKey);
            return Result.Failure<byte[]>(ErrorCodes.CommonUnexpectedError, "File storage is temporarily unavailable.");
        }
    }

    public async Task<Result> UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,
                DisablePayloadSigning = true
            }, cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "R2 upload failed for object {ObjectKey}.", objectKey);
            return Result.Failure(ErrorCodes.CommonUnexpectedError, "File storage is temporarily unavailable.");
        }
    }

    public string CreatePresignedDownloadUrl(string objectKey, string fileName, string contentType, TimeSpan lifetime)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(lifetime)
        };
        request.ResponseHeaderOverrides.ContentType = contentType;
        request.ResponseHeaderOverrides.ContentDisposition = BuildContentDisposition(fileName);
        return _client.GetPreSignedURL(request);
    }

    public string CreatePresignedInlinePdfUrl(string objectKey, TimeSpan lifetime)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(lifetime)
        };
        request.ResponseHeaderOverrides.ContentType = "application/pdf";
        request.ResponseHeaderOverrides.ContentDisposition = "inline";
        return _client.GetPreSignedURL(request);
    }

    internal static string BuildContentDisposition(string fileName)
    {
        var cleaned = new string(Path.GetFileName(fileName).Where(c => !char.IsControl(c)).ToArray());
        var ascii = new string(cleaned.Select(c => c is < ' ' or > '~' or '"' or '\\' or ';' ? '_' : c).ToArray());
        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(cleaned)}";
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return;
        try
        {
            await _client.DeleteObjectAsync(_bucketName, objectKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "R2 object deletion failed for object {ObjectKey}.", objectKey);
        }
    }

    public void Dispose() => _client.Dispose();
}
