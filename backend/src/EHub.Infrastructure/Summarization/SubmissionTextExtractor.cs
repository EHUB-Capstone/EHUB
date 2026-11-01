using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using EHub.Application.Common.Interfaces.Services;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace EHub.Infrastructure.Summarization;

/// <summary>
/// Reads plain text from submitted PDF, DOCX and PPTX files for summarising. XML is parsed without DTDs or external
/// resolution, ZIP entries are size-checked before reading, and reading stops once enough text has been collected.
/// </summary>
public sealed partial class SubmissionTextExtractor : ISubmissionTextExtractor
{
    private const int MaximumXmlBytes = 4 * 1024 * 1024;
    private const int MaximumPdfPages = 200;
    private const int MaximumSlides = 300;

    private static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Presentation = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    public Task<Result<string>> ExtractAsync(byte[] content, string extension, int maxCharacters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (content.Length == 0 || maxCharacters <= 0) return Task.FromResult(Unreadable());
        try
        {
            var text = extension switch
            {
                ".pdf" => ReadPdf(content, maxCharacters, cancellationToken),
                ".docx" => ReadDocx(content, maxCharacters, cancellationToken),
                ".pptx" => ReadPptx(content, maxCharacters, cancellationToken),
                _ => null
            };
            return Task.FromResult(string.IsNullOrWhiteSpace(text) ? Unreadable() : Result.Success(text));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Parser exceptions can contain document contents. Never log them or expose them.
            return Task.FromResult(Unreadable());
        }
    }

    private static string ReadPdf(byte[] content, int maxCharacters, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(content);
        if (document.IsEncrypted) throw new InvalidDataException();
        var text = new StringBuilder();
        foreach (var page in document.GetPages().Take(MaximumPdfPages))
        {
            cancellationToken.ThrowIfCancellationRequested();
            text.AppendLine(ContentOrderTextExtractor.GetText(page));
            if (text.Length >= maxCharacters) break;
        }
        return Limit(text, maxCharacters);
    }

    private static string ReadDocx(byte[] content, int maxCharacters, CancellationToken cancellationToken)
    {
        using var zip = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
        var document = LoadXml(zip, "word/document.xml") ?? throw new InvalidDataException();
        var text = new StringBuilder();
        // Paragraphs include table cells; runs are joined so formatting does not split Vietnamese words.
        foreach (var paragraph in document.Descendants(Word + "p"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var node in paragraph.Descendants())
            {
                if (node.Name == Word + "t") text.Append(node.Value);
                else if (node.Name == Word + "tab") text.Append('\t');
                else if (node.Name == Word + "br" || node.Name == Word + "cr") text.Append('\n');
            }
            text.Append('\n');
            if (text.Length >= maxCharacters) break;
        }
        return Limit(text, maxCharacters);
    }

    private static string ReadPptx(byte[] content, int maxCharacters, CancellationToken cancellationToken)
    {
        using var zip = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
        var text = new StringBuilder();
        var number = 0;
        foreach (var path in SlidePaths(zip).Take(MaximumSlides))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slide = LoadXml(zip, path);
            if (slide is null) continue;
            number++;
            text.Append("[Slide ").Append(number).Append("]\n");
            foreach (var paragraph in slide.Descendants(Drawing + "p"))
            {
                var line = string.Concat(paragraph.Descendants(Drawing + "t").Select(item => item.Value));
                if (!string.IsNullOrWhiteSpace(line)) text.Append(line).Append('\n');
            }
            text.Append('\n');
            if (text.Length >= maxCharacters) break;
        }
        return Limit(text, maxCharacters);
    }

    /// <summary>Slides in presentation order (the file numbering changes meaning once slides are reordered).</summary>
    private static IEnumerable<string> SlidePaths(ZipArchive zip)
    {
        var presentation = LoadXml(zip, "ppt/presentation.xml");
        var relations = LoadXml(zip, "ppt/_rels/presentation.xml.rels");
        if (presentation is not null && relations is not null)
        {
            var targets = relations.Descendants(PackageRelationships + "Relationship")
                .Where(item => item.Attribute("Id") is not null && item.Attribute("Target") is not null)
                .GroupBy(item => item.Attribute("Id")!.Value)
                .ToDictionary(group => group.Key, group => group.First().Attribute("Target")!.Value);
            var ordered = presentation.Descendants(Presentation + "sldId")
                .Select(item => item.Attribute(Relationships + "id")?.Value)
                .Where(id => id is not null && targets.ContainsKey(id))
                .Select(id => "ppt/" + targets[id!].TrimStart('/').Replace("ppt/", string.Empty, StringComparison.Ordinal))
                .ToArray();
            if (ordered.Length > 0) return ordered;
        }
        return zip.Entries.Select(entry => entry.FullName)
            .Where(name => SlideName().IsMatch(name))
            .OrderBy(name => int.Parse(SlideName().Match(name).Groups[1].Value));
    }

    private static XDocument? LoadXml(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path);
        if (entry is null) return null;
        // Uncompressed size is checked first so a small ZIP cannot expand into a huge document.
        if (entry.Length > MaximumXmlBytes) throw new InvalidDataException();
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumXmlBytes
        });
        return XDocument.Load(reader);
    }

    private static string Limit(StringBuilder text, int maxCharacters) =>
        text.Length <= maxCharacters ? text.ToString() : text.ToString(0, maxCharacters);

    private static Result<string> Unreadable() => Result.Failure<string>(ErrorCodes.WorkspaceSummaryUnreadable,
        "The file has no readable text. Scanned PDFs, images and password-protected files are not supported.");

    [GeneratedRegex(@"^ppt/slides/slide(\d+)\.xml$")]
    private static partial Regex SlideName();
}
