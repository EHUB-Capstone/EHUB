using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Services;

/// <param name="Text">The document's text, at most the requested number of characters.</param>
/// <param name="Pages">PDF pages or slides read (0 for Word documents, which have no fixed pages).</param>
/// <param name="PageTexts">The text of each page (PDF) or slide (PPTX) in order, when the format has pages; null otherwise.</param>
public sealed record ExtractedText(string Text, int Pages, IReadOnlyList<string>? PageTexts = null);

public interface ISubmissionTextExtractor
{
    /// <summary>
    /// Reads plain text from a submitted PDF, DOCX or PPTX, stopping after <paramref name="maxCharacters"/>.
    /// Unreadable, encrypted or image-only documents fail with <c>WorkspaceSummaryUnreadable</c>.
    /// </summary>
    Task<Result<ExtractedText>> ExtractAsync(byte[] content, string extension, int maxCharacters, CancellationToken cancellationToken);
}
