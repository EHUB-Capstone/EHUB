using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace EHub.Infrastructure.Mentoring;

public sealed class MentorCvTextReader(IDocumentPreviewConverter converter) : IMentorCvTextReader
{
    private const int MaximumTextLength = 60_000;
    private const int MaximumXmlBytes = 2 * 1024 * 1024;

    public async Task<Result<string>> ReadAsync(byte[] content, string extension, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (content.Length is <= 0 or > 10 * 1024 * 1024) return Invalid();
        try
        {
            string text;
            if (extension == ".docx") text = ReadDocx(content, cancellationToken);
            else if (extension is ".pdf" or ".doc")
            {
                if (extension == ".doc")
                {
                    var converted = await converter.ConvertToPdfAsync(content, extension, cancellationToken);
                    if (converted.IsFailure)
                        return converted.Error.Code == ErrorCodes.WorkspaceFilePreviewUnavailable
                            ? Result.Failure<string>(ErrorCodes.MentorCvExtractionUnavailable,
                                "Chưa thể đọc file DOC. Vui lòng thử lại hoặc chuyển CV sang DOCX/PDF có văn bản.")
                            : Invalid();
                    content = converted.Value.Content;
                }
                text = ReadPdf(content, cancellationToken);
            }
            else return Invalid();
            return string.IsNullOrWhiteSpace(text)
                ? Result.Failure<string>(ErrorCodes.CommonValidationError,
                    "CV không có văn bản đọc được. PDF scan/ảnh cần được OCR hoặc chuyển sang DOCX trước khi tải lên.")
                : Result.Success(text);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Parser exceptions can contain document contents. Never log them or expose them to the client.
            return Invalid();
        }
    }

    public async Task<Result<byte[]>> PreviewPdfAsync(byte[] content, string extension, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (content.Length is <= 0 or > 10 * 1024 * 1024 || extension is not (".pdf" or ".doc" or ".docx"))
            return Result.Failure<byte[]>(ErrorCodes.CommonValidationError,
                "Không thể xem trước CV. Hãy dùng PDF, DOC hoặc DOCX hợp lệ, tối đa 10 MB.");
        if (extension == ".pdf") return Result.Success(content);

        var converted = await converter.ConvertToPdfAsync(content, extension, cancellationToken);
        return converted.IsSuccess
            ? Result.Success(converted.Value.Content)
            : Result.Failure<byte[]>(ErrorCodes.MentorCvExtractionUnavailable,
                "Chưa thể tạo bản xem trước CV. Vui lòng tải file xuống để xem.");
    }

    private static string ReadDocx(byte[] content, CancellationToken cancellationToken)
    {
        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException();
        if (entry.Length > MaximumXmlBytes) throw new InvalidDataException();
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumXmlBytes
        });
        var document = XDocument.Load(reader);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        if (document.Root?.Name != word + "document") throw new InvalidDataException();
        var text = new StringBuilder();
        // Paragraphs also include table cells; text runs are joined so formatting does not split Vietnamese words.
        foreach (var paragraph in document.Descendants(word + "p"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var node in paragraph.Descendants())
            {
                if (node.Name == word + "t") text.Append(node.Value);
                else if (node.Name == word + "tab") text.Append('\t');
                else if (node.Name == word + "br" || node.Name == word + "cr") text.Append('\n');
            }
            text.AppendLine();
            if (text.Length > MaximumTextLength) throw new InvalidDataException();
        }
        return text.ToString();
    }

    private static string ReadPdf(byte[] content, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(content);
        if (document.IsEncrypted || document.NumberOfPages > 40) throw new InvalidDataException();
        var text = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            text.AppendLine(ContentOrderTextExtractor.GetText(page));
            if (text.Length > MaximumTextLength) throw new InvalidDataException();
        }
        return text.ToString();
    }

    private static Result<string> Invalid() => Result.Failure<string>(ErrorCodes.CommonValidationError,
        "Không đọc được CV. Hãy dùng PDF/DOC/DOCX hợp lệ, không đặt mật khẩu, tối đa 40 trang và 60.000 ký tự.");
}
