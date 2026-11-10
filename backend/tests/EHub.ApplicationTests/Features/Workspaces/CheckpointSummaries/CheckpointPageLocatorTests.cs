using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

/// <summary>A reviewer opens a quote at its page or slide; only PDF and slides have pages.</summary>
public sealed class CheckpointPageLocatorTests
{
    private static readonly ExtractedText Slides = new("ignored", 3,
    [
        "Giới thiệu nhóm\nEcoBag là túi vải tái chế cho sinh viên.",
        "Đối thủ cạnh tranh\nĐối thủ trực tiếp là túi vải in sẵn trên Shopee giá 25.000–35.000 đồng.",
        "Kế hoạch\nTuần 1–2: khảo sát 100 sinh viên quận Thủ Đức."
    ]);

    [Fact]
    public void Locate_FindsThePageOfAQuoteEvenWhenTheTextWasTidied()
    {
        var locator = CheckpointPageLocator.From(Slides)!;

        locator.Locate("Đối thủ trực tiếp là túi vải in sẵn trên Shopee giá 25.000–35.000 đồng.").Should().Be(2);
        // A quote joined from several lines is placed by its first part.
        locator.Locate("Kế hoạch … Tuần 1–2: khảo sát 100 sinh viên quận Thủ Đức.").Should().Be(3);
        // Spaces do not matter: a syllable the PDF split ("bằ ng") reads the same as the repaired quote ("bằng").
        CheckpointPageLocator.From(new ExtractedText("x", 1, ["Phương án thay thế bằ ng giải pháp số hóa cho doanh nghiệp."]))!
            .Locate("Phương án thay thế bằng giải pháp số hóa cho doanh nghiệp.").Should().Be(1);
    }

    [Fact]
    public void Locate_ReturnsNullWhenItCannotTell()
    {
        var locator = CheckpointPageLocator.From(Slides)!;

        locator.Locate(null).Should().BeNull();
        locator.Locate("Ngắn").Should().BeNull("too short to place");
        locator.Locate("Câu này không có trong bất kỳ trang nào của tệp").Should().BeNull();
        CheckpointPageLocator.From(new ExtractedText("Word file", 0)).Should().BeNull("a Word file has no pages to give");
    }

    [Fact]
    public void Preview_GivesEvidenceAndKeySentencesTheirPage_AndCombineKeepsTheFile()
    {
        ChecklistItem[] checklist = [new(0, "Criterion", "Competitor Analysis", "Competitors are analysed with evidence", 20, "Document", "Competitors are analysed with evidence")];
        const string text = "Giới thiệu nhóm\nEcoBag là túi vải tái chế cho sinh viên.\nĐối thủ cạnh tranh\nĐối thủ trực tiếp là túi vải in sẵn trên Shopee giá 25.000–35.000 đồng.";

        var preview = CheckpointSummaryEvidence.Preview(text, checklist, CheckpointSummaryTopics.None, false, 0, CheckpointPageLocator.From(Slides));

        preview.Requirements.Single().EvidencePage.Should().Be(2);
        preview.HighlightPages.Should().HaveCount(preview.Highlights.Count);
        var fileId = Guid.NewGuid();
        var combined = CheckpointSummaryOutput.CombineWithFiles(checklist, [((Guid?)fileId, "Slides.pptx", preview.Requirements)]).Single();
        combined.SourceFileId.Should().Be(fileId);
        combined.EvidencePage.Should().Be(2);
        combined.Source.Should().Be("Slides.pptx");
    }
}
