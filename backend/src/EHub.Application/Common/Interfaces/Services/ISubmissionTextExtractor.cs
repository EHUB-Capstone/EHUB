using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Services;

public interface ISubmissionTextExtractor
{
    /// <summary>
    /// Reads plain text from a submitted PDF, DOCX or PPTX, stopping after <paramref name="maxCharacters"/>.
    /// Unreadable, encrypted or image-only documents fail with <c>WorkspaceSummaryUnreadable</c>.
    /// </summary>
    Task<Result<string>> ExtractAsync(byte[] content, string extension, int maxCharacters, CancellationToken cancellationToken);
}
