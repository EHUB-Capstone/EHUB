using System.IO.Compression;
using System.Text;
using EHub.Infrastructure.Summarization;
using EHub.Shared.Errors;
using FluentAssertions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class SubmissionTextExtractorTests
{
    private const string WordNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private readonly SubmissionTextExtractor _extractor = new();

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        return stream.ToArray();
    }

    private static byte[] Docx(params string[] paragraphs) => Zip(("word/document.xml",
        $"<w:document xmlns:w=\"{WordNs}\"><w:body>" +
        string.Concat(paragraphs.Select(p => $"<w:p><w:r><w:t>{p[..(p.Length / 2)]}</w:t></w:r><w:r><w:t>{p[(p.Length / 2)..]}</w:t></w:r></w:p>")) +
        "</w:body></w:document>"));

    private static string Slide(string text) =>
        $"<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"{DrawingNs}\">" +
        $"<p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>{text}</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>";

    [Fact]
    public async Task Docx_JoinsRunsAndKeepsVietnameseText()
    {
        var result = await _extractor.ExtractAsync(Docx("Khách hàng mục tiêu là sinh viên", "Giá bán 45.000 đồng"), ".docx", 10_000, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Khách hàng mục tiêu là sinh viên").And.Contain("Giá bán 45.000 đồng");
    }

    [Fact]
    public async Task Pptx_ReadsSlidesInPresentationOrderNotFileOrder()
    {
        var pptx = Zip(
            ("ppt/presentation.xml",
                "<p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" " +
                "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                "<p:sldIdLst><p:sldId id=\"256\" r:id=\"rId3\"/><p:sldId id=\"257\" r:id=\"rId2\"/></p:sldIdLst></p:presentation>"),
            ("ppt/_rels/presentation.xml.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId2\" Target=\"slides/slide1.xml\"/><Relationship Id=\"rId3\" Target=\"slides/slide2.xml\"/></Relationships>"),
            ("ppt/slides/slide1.xml", Slide("Kế hoạch gọi vốn")),
            ("ppt/slides/slide2.xml", Slide("Vấn đề của khách hàng")));

        var result = await _extractor.ExtractAsync(pptx, ".pptx", 10_000, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IndexOf("Vấn đề của khách hàng", StringComparison.Ordinal)
            .Should().BeLessThan(result.Value.IndexOf("Kế hoạch gọi vốn", StringComparison.Ordinal));
        result.Value.Should().StartWith("[Slide 1]");
    }

    [Fact]
    public async Task Pdf_ReadsTextFromPages()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText("Survey of 150 students in Ho Chi Minh City", 12, new PdfPoint(40, 700), font);

        var result = await _extractor.ExtractAsync(builder.Build(), ".pdf", 10_000, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("150 students");
    }

    [Fact]
    public async Task StopsAtTheCharacterLimit()
    {
        var result = await _extractor.ExtractAsync(Docx(Enumerable.Range(1, 500).Select(i => $"Đoạn văn số {i} về kế hoạch").ToArray()), ".docx", 1_000, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Length.Should().Be(1_000);
    }

    [Theory]
    [InlineData(".docx")]
    [InlineData(".pptx")]
    [InlineData(".pdf")]
    [InlineData(".xlsx")]
    public async Task InvalidOrUnsupportedFiles_AreUnreadable(string extension)
    {
        var result = await _extractor.ExtractAsync("not a document"u8.ToArray(), extension, 1_000, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryUnreadable);
    }

    [Fact]
    public async Task DocumentTypeDefinitions_AreRejected()
    {
        var docx = Zip(("word/document.xml",
            $"<!DOCTYPE w:document [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><w:document xmlns:w=\"{WordNs}\"><w:body><w:p><w:r><w:t>&x;</w:t></w:r></w:p></w:body></w:document>"));

        var result = await _extractor.ExtractAsync(docx, ".docx", 1_000, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.WorkspaceSummaryUnreadable);
    }

    [Fact]
    public async Task DocumentWithoutText_IsUnreadable()
    {
        var result = await _extractor.ExtractAsync(Docx(), ".docx", 1_000, default);

        result.IsFailure.Should().BeTrue();
    }
}
