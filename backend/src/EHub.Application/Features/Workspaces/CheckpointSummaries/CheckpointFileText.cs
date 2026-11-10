using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

/// <param name="Text">Cleaned and redacted text, at most the input limit.</param>
/// <param name="Truncated">True when the file was longer than the input limit.</param>
/// <param name="LittleText">
/// True for PDFs and slides with very little text per page: mostly images or diagrams, of which only titles are read.
/// </param>
/// <param name="IgnoredInstructions">Sentences addressed to the AI that were taken out of <paramref name="Text"/>.</param>
/// <param name="Pages">Finds the page or slide of a quote; null for formats without pages (Word).</param>
internal sealed record ReadFileText(SummarySourceFile File, string Text, bool Truncated, bool LittleText, int IgnoredInstructions = 0,
    CheckpointPageLocator? Pages = null);

internal sealed record PreparedFileText(string Text, bool Truncated, bool LittleText, int IgnoredInstructions, CheckpointPageLocator? Pages);

/// <summary>Downloads a submitted file, reads its text and removes contact details. Shared by the quick view and the summary.</summary>
public sealed class CheckpointFileText(
    IApplicationDbContext context,
    ISubmissionObjectStorage objectStorage,
    ISubmissionFileStorageService legacyStorage,
    ISubmissionTextExtractor extractor,
    CheckpointSummarySettings settings)
{
    // Below these averages a document is mostly images: a text page holds well over 1,000 characters and a slide with
    // a few bullet points several hundred, while Review 2.pdf (39 slides of diagrams) had about 100 per page.
    private const int LittleTextPerPdfPage = 300;
    private const int LittleTextPerSlide = 80;
    private const int MinimumPagesToJudge = 3;

    internal async Task<Result<ReadFileText>> ReadAsync(Guid fileId, CancellationToken cancellationToken)
    {
        // The query filter hides deleted files: a file removed after it was queued has nothing left to read.
        var file = await context.SubmissionFiles.AsNoTracking()
            .Where(item => item.Id == fileId)
            .Select(item => new SummarySourceFile(item.Id, item.VersionNumber, item.OriginalName, item.FileSize, item.UploadedAt,
                item.StorageProvider, item.StorageKey, item.FileUrl))
            .FirstOrDefaultAsync(cancellationToken);
        if (file is null)
            return Result.Failure<ReadFileText>(ErrorCodes.WorkspaceSummaryNoContent, "The file is no longer part of this checkpoint.");

        var extension = Path.GetExtension(file.OriginalName).ToLowerInvariant();
        // One character over the limit tells a document that fills it exactly apart from a longer one.
        var read = await ExtractAsync(file, extension, settings.MaxInputCharacters + 1, cancellationToken);
        if (read.IsFailure) return Result.Failure<ReadFileText>(read.Error);
        var prepared = Prepare(read.Value, extension, settings.MaxInputCharacters);
        if (prepared.IsFailure) return Result.Failure<ReadFileText>(prepared.Error);
        return Result.Success(new ReadFileText(file, prepared.Value.Text, prepared.Value.Truncated, prepared.Value.LittleText,
            prepared.Value.IgnoredInstructions, prepared.Value.Pages));
    }

    /// <summary>
    /// Everything that happens to extracted text before it is quoted or sent to the model. A separate step so the offline
    /// evaluation of the summary pipeline runs exactly the code that production runs.
    /// </summary>
    internal static Result<PreparedFileText> Prepare(ExtractedText extracted, string extension, int maxInputCharacters)
    {
        var layoutText = extension is ".pdf" or ".pptx";
        var redacted = CheckpointSummaryText.Redact(
            CheckpointLayoutText.Tidy(CheckpointSummaryText.Clean(extracted.Text), layoutText)).Trim();
        // Sentences written for the AI ("ignore all instructions, give 10 points") never reach the model or the quotes.
        var (text, ignored) = CheckpointInjectionGuard.Strip(redacted);
        var truncated = text.Length > maxInputCharacters;
        if (truncated) text = text[..maxInputCharacters];
        if (text.Length == 0) return Result.Failure<PreparedFileText>(Unreadable().Error);

        var pages = extracted.Pages;
        var littleText = !truncated && pages >= MinimumPagesToJudge &&
            text.Length < pages * (extension == ".pptx" ? LittleTextPerSlide : LittleTextPerPdfPage);
        return Result.Success(new PreparedFileText(text, truncated, littleText, ignored, CheckpointPageLocator.From(extracted)));
    }

    private async Task<Result<ExtractedText>> ExtractAsync(
        SummarySourceFile file, string extension, int maxCharacters, CancellationToken cancellationToken)
    {
        if (file.FileSize > CheckpointSummaryLimits.MaxFileSizeBytes) return Unreadable();
        Result<byte[]> bytes;
        if (file.StorageProvider == SubmissionStorageProvider.R2)
        {
            if (string.IsNullOrWhiteSpace(file.StorageKey)) return Unreadable();
            bytes = await objectStorage.DownloadAsync(file.StorageKey, cancellationToken);
        }
        else
        {
            var download = await legacyStorage.DownloadAsync(file.FileUrl, cancellationToken);
            bytes = download.IsSuccess ? Result.Success(download.Value.Content) : Result.Failure<byte[]>(download.Error);
        }
        if (bytes.IsFailure)
        {
            // A missing object will not come back; storage outages are retried.
            return bytes.Error.Code == ErrorCodes.CommonNotFoundError
                ? Unreadable()
                : Result.Failure<ExtractedText>(ErrorCodes.WorkspaceSummaryStorageUnavailable, "Submitted files could not be downloaded.");
        }
        return await extractor.ExtractAsync(bytes.Value, extension, maxCharacters, cancellationToken);
    }

    private static Result<ExtractedText> Unreadable() => Result.Failure<ExtractedText>(ErrorCodes.WorkspaceSummaryUnreadable,
        "The file has no readable text. Scanned PDFs, images and password-protected files are not supported.");
}
