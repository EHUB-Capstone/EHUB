using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Storage;

public interface IDocumentPreviewConverter
{
    Task<Result<DocumentPreviewConversionResult>> ConvertToPdfAsync(
        byte[] sourceContent,
        string sourceExtension,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentPreviewConversionResult(byte[] Content);
