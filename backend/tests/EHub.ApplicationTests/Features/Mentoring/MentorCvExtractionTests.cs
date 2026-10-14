using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Mentoring;
using EHub.Application.Features.Mentoring.ExtractCv;
using EHub.Domain.Entities;
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace EHub.ApplicationTests.Features.Mentoring;

public sealed class MentorCvExtractionTests
{
    // Entirely synthetic. No details from a real employee/CV are used as test fixtures.
    private const string EmployeeForm = """
        PHIẾU THÔNG TIN NHÂN VIÊN
        I. THÔNG TIN CÁ NHÂN
        Họ và tên: PRIVATE_PERSON_MARKER
        Email: private-marker@example.test
        Số CCCD: PRIVATE_ID_MARKER
        II. THÔNG TIN NGƯỜI THÂN
        PRIVATE_FAMILY_MARKER
        III. QUÁ TRÌNH ĐÀO TẠO
        09/2010-05/2014
        Đại học Demo
        Tài chính
        Cử nhân
        IV. KINH NGHIỆM LÀM VIỆC
        12/2023-nay
        Demo Growth
        Giám đốc Marketing
        Phụ trách kinh doanh và chiến lược marketing
        2015 - 2020
        Demo Operations
        Giám đốc Vận hành
        Điều phối dự án
        V. TRÌNH ĐỘ NGOẠI NGỮ & VI TÍNH
        Tiếng Anh
        TOEFL ibt
        100
        VII. CÁC THÀNH TÍCH, GIẢI THƯỞNG
        Giải thưởng chuyên môn Demo
        6.2. Sở thích:
        PRIVATE_HOBBY_MARKER
        VIII. CÁC CÔNG TRÌNH NGHIÊN CỨU
        Nghiên cứu Marketing
        Hà Nội, ngày 01 tháng 01 năm 2026
        NGƯỜI KHAI
        PRIVATE_SIGNATURE_MARKER
        """;

    [Fact]
    public void EmployeeFormExtractsProfessionalSectionsAndExcludesPrivateSections()
    {
        var result = MentorCvProfileParser.Parse(EmployeeForm);
        result.IsSuccess.Should().BeTrue();
        result.Value.Bio.Should().Contain("Đại học Demo").And.Contain("TOEFL");
        result.Value.Experience.Should().Contain("Demo Growth").And.Contain("Giám đốc Marketing")
            .And.Contain("2015 - 2020");
        result.Value.Expertise.Should().Contain(["Marketing", "Finance", "Operations management", "Project management", "Strategy"]);
        (result.Value.Bio + result.Value.Experience).Should().NotContain("PRIVATE_").And.NotContain("private-marker");
    }

    [Theory]
    [InlineData("Skills: React, TypeScript, C#; cybersecurity")]
    [InlineData("Technical skills\nReact, TypeScript, C#; cybersecurity")]
    [InlineData("Vi tính\nReact, TypeScript, C#; cybersecurity")]
    [InlineData("VI. KỸ NĂNG\nReact, TypeScript, C#; cybersecurity")]
    public void SkillsSectionsAreExcludedWhileEducationAndExperienceAreExtracted(string skills)
    {
        var result = MentorCvProfileParser.Parse($"Personal information\nPRIVATE_MARKER\nEducation\nDemo University\n{skills}\nWork experience\nMarketing director\nReferences\nPRIVATE_REFERENCE");
        result.IsSuccess.Should().BeTrue();
        result.Value.Experience.Should().Be("Marketing director");
        result.Value.Bio.Should().Be("Demo University");
        result.Value.Expertise.Should().Equal("Marketing");
        MentorCvProfileParser.Parse(skills).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void EducationAndExperienceKeepTechnologyNarrativeWithoutASkillsSection()
    {
        var result = MentorCvProfileParser.Parse("Education\nDemo University - Software engineering\nWork experience\nBuilt React applications with PostgreSQL");
        result.IsSuccess.Should().BeTrue();
        result.Value.Bio.Should().Be("Demo University - Software engineering");
        result.Value.Experience.Should().Be("Built React applications with PostgreSQL");
        result.Value.Expertise.Should().Contain("Software engineering");
    }

    [Fact]
    public void MissingProfessionalSectionsFailsAndLongSectionsRespectProfileLimits()
    {
        MentorCvProfileParser.Parse("Personal information\nPRIVATE_MARKER").IsFailure.Should().BeTrue();
        var result = MentorCvProfileParser.Parse("Education\n" + new string('a', 3000) + "\nWork experience\n" + new string('b', 5000));
        result.Value.Bio.Should().HaveLength(2000);
        result.Value.Experience.Should().HaveLength(4000);
    }

    [Fact]
    public async Task DocxReaderPreservesVietnameseRunsAndTableParagraphs()
    {
        var reader = new MentorCvTextReader(Substitute.For<IDocumentPreviewConverter>());
        var result = await reader.ReadAsync(Docx(EmployeeForm), ".docx", default);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("QUÁ TRÌNH ĐÀO TẠO").And.Contain("Giám đốc Marketing");
        MentorCvProfileParser.Parse(result.Value).Value.Expertise.Should().Contain("Finance");
    }

    [Fact]
    public async Task PdfReaderExtractsRealPdfTextAndRejectsEmptyAndMalformedPdf()
    {
        var reader = new MentorCvTextReader(Substitute.For<IDocumentPreviewConverter>());
        var result = await reader.ReadAsync(Pdf("Education", "Demo University", "Work experience", "Marketing director"), ".pdf", default);
        result.Value.Should().Contain("Demo University");
        MentorCvProfileParser.Parse(result.Value).Value.Experience.Should().Contain("Marketing director");
        (await reader.ReadAsync(Pdf(), ".pdf", default)).Error.Message.Should().Contain("OCR");
        (await reader.ReadAsync("%PDF-not-a-real-pdf"u8.ToArray(), ".pdf", default)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DocReaderUsesConverterAndReportsUnavailabilitySafely()
    {
        var converter = Substitute.For<IDocumentPreviewConverter>();
        var source = new byte[] { 1, 2, 3 };
        converter.ConvertToPdfAsync(source, ".doc", default).Returns(Result.Success(new DocumentPreviewConversionResult(Pdf("Work experience", "Marketing director"))));
        var reader = new MentorCvTextReader(converter);
        (await reader.ReadAsync(source, ".doc", default)).Value.Should().Contain("Marketing director");
        converter.ConvertToPdfAsync(source, ".doc", default).Returns(Result.Failure<DocumentPreviewConversionResult>(ErrorCodes.WorkspaceFilePreviewUnavailable, "internal details"));
        var unavailable = await reader.ReadAsync(source, ".doc", default);
        unavailable.Error.Code.Should().Be(ErrorCodes.MentorCvExtractionUnavailable);
        unavailable.Error.Message.Should().NotContain("internal details");
    }

    [Fact]
    public async Task DocxReaderRejectsDtdAndOversizedExpandedXmlAndHonorsCancellation()
    {
        var reader = new MentorCvTextReader(Substitute.For<IDocumentPreviewConverter>());
        (await reader.ReadAsync(ZipXml("<!DOCTYPE document [<!ENTITY xxe SYSTEM 'file:///PRIVATE_MARKER'>]><document>&xxe;</document>"), ".docx", default)).IsFailure.Should().BeTrue();
        (await reader.ReadAsync(ZipXml(new string('a', 2 * 1024 * 1024 + 1)), ".docx", default)).IsFailure.Should().BeTrue();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var action = () => reader.ReadAsync(Docx(EmployeeForm), ".docx", cancelled.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task UploadExtractsSavesDownloadsAndKeepsAdminAndManualFields()
    {
        await using var db = Context();
        var profile = Profile();
        db.MentorProfiles.Add(profile);
        await db.SaveChangesAsync();
        var bytes = Docx(EmployeeForm);
        var storage = Substitute.For<IMentorDocumentStorageService>();
        storage.UploadAsync(Arg.Any<Stream>(), "cv.docx", profile.Id, "cv", default)
            .Returns(Result.Success(("https://storage.example.test/cv", "new-cv")));
        storage.DownloadAsync("https://storage.example.test/cv", default).Returns(Result.Success(bytes));
        var handler = new MentorProfileHandler(db, storage, Substitute.For<IMentorEmbeddingSearch>(),
            new MentorCvTextReader(Substitute.For<IDocumentPreviewConverter>()));
        using var stream = new MemoryStream(bytes);
        var result = await handler.UploadDocumentAsync(profile.UserId, "cv", stream, "cv.docx", bytes.Length, default);
        result.IsSuccess.Should().BeTrue();
        result.Value.Experience.Should().Contain("Giám đốc Marketing").And.NotContain("PRIVATE");
        result.Value.Bio.Should().Contain("Đại học Demo");
        result.Value.Expertise.Should().Contain(["Manual skill", "Marketing", "Finance"]);
        result.Value.MentorType.Should().Be("Business");
        result.Value.Organization.Should().Be("Manual organization");
        result.Value.Tags.Should().Equal("Manual tag");
        result.Value.TechnologySkills.Should().Equal("Negotiation", "Python");
        db.ChangeTracker.Clear();
        var saved = await db.MentorProfiles.SingleAsync();
        saved.Experience.Should().Be(result.Value.Experience);
        saved.TechnologySkills.Should().Equal("Negotiation", "Python");
        var download = await handler.DownloadDocumentAsync(profile.Id, "cv", profile.UserId, SystemRoles.Mentor, default);
        download.Value.Content.Should().Equal(bytes);
        download.Value.ContentType.Should().Contain("wordprocessingml");
        (await handler.DownloadDocumentAsync(profile.Id, "cv", Guid.NewGuid(), SystemRoles.Mentor, default)).Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Theory]
    [InlineData("cv.docx", true)]
    [InlineData("cv.pdf", false)]
    [InlineData("cv.exe", false)]
    public async Task FailedExtractionDoesNotUploadOrReplaceExistingCv(string name, bool noProfessionalSections)
    {
        await using var db = Context();
        var profile = Profile();
        profile.CvFileName = "previous.pdf";
        profile.Experience = "Previous experience";
        db.MentorProfiles.Add(profile);
        await db.SaveChangesAsync();
        var storage = Substitute.For<IMentorDocumentStorageService>();
        var handler = new MentorProfileHandler(db, storage, Substitute.For<IMentorEmbeddingSearch>(),
            new MentorCvTextReader(Substitute.For<IDocumentPreviewConverter>()));
        var bytes = noProfessionalSections ? Docx("Personal information\nPRIVATE_MARKER") : Encoding.UTF8.GetBytes("invalid document");
        using var stream = new MemoryStream(bytes);
        (await handler.UploadDocumentAsync(profile.UserId, "cv", stream, name, bytes.Length, default)).IsFailure.Should().BeTrue();
        await storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default, default!, default);
        db.ChangeTracker.Clear();
        var unchanged = await db.MentorProfiles.SingleAsync();
        unchanged.CvFileName.Should().Be("previous.pdf");
        unchanged.Experience.Should().Be("Previous experience");
        unchanged.TechnologySkills.Should().Equal("Negotiation", "Python");
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static MentorProfile Profile() => new()
    {
        User = new User { Email = "cv-test@example.test", FullName = "Synthetic mentor" },
        Expertise = ["Manual skill"], TechnologySkills = ["Negotiation", "Python"],
        Tags = ["Manual tag"], Organization = "Manual organization"
    };

    private static byte[] Docx(string text)
    {
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = text.Split('\n').Select(line => new XElement(word + "tr", new XElement(word + "tc",
            new XElement(word + "p", new XElement(word + "r", new XElement(word + "t", line[..(line.Length / 2)])),
                new XElement(word + "r", new XElement(word + "t", line[(line.Length / 2)..]))))));
        return ZipXml(new XDocument(new XElement(word + "document", new XElement(word + "body", new XElement(word + "tbl", paragraphs)))).ToString());
    }

    private static byte[] ZipXml(string xml)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open(), new UTF8Encoding(false)))
            writer.Write(xml);
        return memory.ToArray();
    }

    private static byte[] Pdf(params string[] lines)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(600, 800);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var index = 0; index < lines.Length; index++)
            page.AddText(lines[index], 12, new PdfPoint(30, 750 - 20 * index), font);
        return builder.Build();
    }
}
