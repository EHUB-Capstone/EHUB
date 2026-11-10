using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

// A requirement about a product that was built ("MVP hoặc prototype đã xây dựng") must not be shown as covered by a cover page, a
// research goal or a bare title. Text is invented.
public sealed class CheckpointProductEvidenceTests
{
    private static readonly ChecklistItem[] Requirement = [new(0, "Requirement", "MVP hoặc prototype đã xây dựng", null)];

    private static string FindingOf(string text) =>
        CheckpointSummaryEvidence.Find(text, Requirement, CheckpointSummaryTopics.None)[0].Finding;

    [Fact]
    public void ACoverPageLineNamingAPersonIsNotEvidenceOfAProduct()
    {
        // "Demo" is a surname here, not a demo.
        var text = "BÁO CÁO KHẢO SÁT KHÁCH HÀNG\nInstructor: Mr. Demo\nLớp: SE1801\nTổng quan phương pháp và mẫu khảo sát\nKhảo sát 120 sinh viên trong tháng 10.";

        FindingOf(text).Should().Be(CheckpointSummaryEvidence.NotFound);
    }

    [Fact]
    public void AResearchGoalToDefineAnMvpIsNotAnMvp()
    {
        var text = "Mục tiêu nghiên cứu: đánh giá khả năng gia nhập thị trường và xác định MVP.\nKhảo sát 120 sinh viên về thói quen học nhóm trong tháng 10.";

        FindingOf(text).Should().Be(CheckpointSummaryEvidence.NotFound);
    }

    [Fact]
    public void ABareOneWordTitleIsNotEvidenceOfAProduct()
    {
        FindingOf("BOOKEASY\nDEMO\nHình 1\nHình 2").Should().Be(CheckpointSummaryEvidence.NotFound);
    }

    [Fact]
    public void ASentenceSayingWhatWasBuiltStillCounts()
    {
        FindingOf("Nhóm đã xây dựng prototype trên Figma gồm 9 màn hình: tìm tiệm, chọn dịch vụ, đặt giờ, thanh toán.")
            .Should().Be(CheckpointSummaryEvidence.Related);
        FindingOf("MVP đã chạy được với 5 màn hình và 12 người dùng thử trong tuần qua.").Should().Be(CheckpointSummaryEvidence.Related);
    }

    [Fact]
    public void AOneWordSlideTitleStillLendsItsWeightToTheLinesUnderIt()
    {
        // "Prototype" alone proves nothing, but the slide under it is where the quote comes from, not the later test slide.
        var text = "Prototype\nPrototype Figma gồm 9 màn hình: tìm tiệm, chọn dịch vụ, đặt giờ, thanh toán, đánh giá\n" +
                   "Thử nghiệm prototype\n8 chủ nuôi thử bấm theo kịch bản đặt lịch\n6/8 người hoàn thành đặt lịch dưới 2 phút";

        var finding = CheckpointSummaryEvidence.Find(text, Requirement, CheckpointSummaryTopics.None)[0];

        finding.Finding.Should().Be(CheckpointSummaryEvidence.Related);
        finding.Evidence.Should().StartWith("Prototype Figma gồm 9 màn hình");
    }

    [Theory]
    [InlineData("Instructor: Mr. Nguyen", true)]
    [InlineData("Giảng viên: Nguyễn Văn A", true)]
    [InlineData("Lớp: SE1801", true)]
    [InlineData("Mentor góp ý rằng nhóm cần làm rõ mô hình doanh thu.", false)]
    [InlineData("Giảng viên đã duyệt hướng đi của nhóm.", false)]
    public void OnlyALabelFollowedByAColonIsCoverMetadata(string sentence, bool expected)
    {
        CheckpointSummaryText.IsCoverMetadata(sentence).Should().Be(expected);
    }
}
