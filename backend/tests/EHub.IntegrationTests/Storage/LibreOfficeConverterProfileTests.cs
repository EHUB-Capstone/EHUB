using System.IO.Compression;
using System.Text;
using EHub.Infrastructure.Options;
using EHub.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EHub.IntegrationTests.Storage;

// Runs the real LibreOffice. When it is not installed (for example on a build agent) the tests return
// early instead of failing, because there is nothing to convert with.
public sealed class LibreOfficeConverterProfileTests
{
    private static readonly string ProfilesRoot =
        Path.Combine(Path.GetTempPath(), "ehub-document-previews", "profiles");

    private static bool LibreOfficeAvailable()
    {
        if (OperatingSystem.IsWindows())
        {
            var windowsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe");
            if (File.Exists(windowsPath)) return true;
        }

        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, OperatingSystem.IsWindows() ? "soffice.exe" : "soffice")));
    }

    private static LibreOfficeDocumentPreviewConverter CreateConverter(int concurrency = 2) =>
        new(Options.Create(new DocumentPreviewOptions { MaximumConcurrentConversions = concurrency }),
            NullLogger<LibreOfficeDocumentPreviewConverter>.Instance);

    private static byte[] BuildDocx(string text)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }

            Add("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Add("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Add("word/document.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:body></w:document>");
        }

        return buffer.ToArray();
    }

    private static bool IsPdf(byte[] content) => content.Length > 5 && Encoding.ASCII.GetString(content, 0, 5) == "%PDF-";

    [Fact]
    public async Task Conversions_ReuseTheSameProfileInsteadOfCreatingOneEachTime()
    {
        if (!LibreOfficeAvailable()) return;
        using var converter = CreateConverter(concurrency: 1);

        var first = await converter.ConvertToPdfAsync(BuildDocx("first"), ".docx");
        var profile = Path.Combine(ProfilesRoot, "slot-0");
        Directory.Exists(profile).Should().BeTrue("the profile is kept for the next conversion");
        var marker = Path.Combine(profile, "reuse-marker.txt");
        await File.WriteAllTextAsync(marker, "still here");
        var second = await converter.ConvertToPdfAsync(BuildDocx("second"), ".docx");
        var third = await converter.ConvertToPdfAsync(BuildDocx("third"), ".docx");

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        third.IsSuccess.Should().BeTrue();
        IsPdf(second.Value.Content).Should().BeTrue();
        IsPdf(third.Value.Content).Should().BeTrue();
        File.Exists(marker).Should().BeTrue("a reused profile is not recreated between conversions");
    }

    [Fact]
    public async Task ConcurrentConversions_NeverShareAProfile()
    {
        if (!LibreOfficeAvailable()) return;
        using var converter = CreateConverter(concurrency: 2);

        var results = await Task.WhenAll(
            converter.ConvertToPdfAsync(BuildDocx("alpha"), ".docx"),
            converter.ConvertToPdfAsync(BuildDocx("beta"), ".docx"));

        results.Should().OnlyContain(result => result.IsSuccess && IsPdf(result.Value.Content));
        Directory.Exists(Path.Combine(ProfilesRoot, "slot-0")).Should().BeTrue();
        Directory.Exists(Path.Combine(ProfilesRoot, "slot-1")).Should().BeTrue();
    }

    [Fact]
    public async Task AFailedConversionDiscardsItsProfileAndTheNextOneStillWorks()
    {
        if (!LibreOfficeAvailable()) return;
        using var converter = CreateConverter(concurrency: 1);
        (await converter.ConvertToPdfAsync(BuildDocx("warm up"), ".docx")).IsSuccess.Should().BeTrue();
        var marker = Path.Combine(ProfilesRoot, "slot-0", "failure-marker.txt");
        await File.WriteAllTextAsync(marker, "profile before failure");

        // A ZIP that is not a document makes LibreOffice fail (or produce nothing).
        var broken = await converter.ConvertToPdfAsync("not a document"u8.ToArray(), ".docx");
        var recovered = await converter.ConvertToPdfAsync(BuildDocx("after failure"), ".docx");

        recovered.IsSuccess.Should().BeTrue();
        IsPdf(recovered.Value.Content).Should().BeTrue();
        if (broken.IsFailure)
        {
            File.Exists(marker).Should().BeFalse("a failed run must not leave a possibly damaged profile in use");
        }
    }
}
