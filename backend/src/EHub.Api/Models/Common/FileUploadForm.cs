using Microsoft.AspNetCore.Http;

namespace EHub.Api.Models.Common;

/// <summary>
/// Multipart payload for endpoints that accept one uploaded file.
/// </summary>
public sealed class FileUploadForm
{
    public IFormFile File { get; init; } = default!;
}
