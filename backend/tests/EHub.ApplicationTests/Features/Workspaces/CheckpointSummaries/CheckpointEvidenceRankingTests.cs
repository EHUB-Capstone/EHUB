using EHub.Application.Features.Mentoring.Evidence;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

/// <summary>Evidence ranking problems seen on slide decks written in Vietnamese (EXE101 Checkpoint 2).</summary>
public sealed class CheckpointEvidenceRankingTests
{
    private static ChecklistItem Criterion(int index, string name, string excellent) =>
        new(index, "Criterion", name, excellent, 20, CheckpointCriterionProfile.Classify(name), excellent);

    private static readonly ChecklistItem Target =
        Criterion(0, "Target Customer Definition", "Target customer is specific, well-segmented, and supported by data");

    private static readonly ChecklistItem ProblemAnalysis =
        Criterion(1, "Market Problem Analysis", "Market problem is analyzed deeply with credible evidence");

    [Fact]
    public void Find_QuotesAMatchingHeadingWithItsLinesInsteadOfAnUnrelatedNeighbour()
    {
        // The line before the heading is about the product's limits; it must not win because it sits next to the heading.
        const string deck =
            "HẠN CHẾ Phụ thuộc mật độ địa lý · Khách ngại bỏ thói quen dùng Excel/Zalo miễn phí\n" +
            "Phân khúc và người tham gia khảo sát\n" +
            "Phân khúc ban đầu: F&B nhiều chi nhánh, nhân viên làm theo ca.\n" +
            "100+ người trả lời, 83% là người trực tiếp quản lý nhân viên.";

        var evidence = CheckpointSummaryEvidence.Find(deck, [Target], CheckpointSummaryTopics.None)[0].Evidence;

        evidence.Should().NotContain("HẠN CHẾ");
        evidence.Should().Contain("Phân khúc ban đầu: F&B nhiều chi nhánh");
    }

    [Fact]
    public void Cues_DoNotTreatAMarketSegmentOrALimitationAsTheCustomerOrTheProblem()
    {
        string[] file =
        [
            "Cloud-SaaS: Phân khúc Xử lý chứng từ thông minh (IDP) tăng trưởng bùng nổ",
            "HẠN CHẾ Phụ thuộc mật độ địa lý"
        ];
        var normalised = file.Select(line => MentorEvidenceText.Normalize(line)).ToArray();

        CheckpointCriterionCues.Find("Target Customer Definition", Target.Excellent, file, normalised)
            .Single().Found.Should().BeFalse("a software market segment is not the team's customer segment");
        CheckpointCriterionCues.Find("Market Problem Analysis", ProblemAnalysis.Excellent, file, normalised)
            .Single(cue => cue.Label == "Problem").Found.Should().BeFalse("limits of the product are not the customers' problem");
        // A customer segment written in Vietnamese still counts, even with a broken diacritic from the PDF.
        string[] real = ["Phân khúc ban đầ u: F&B nhiều chi nhánh, nhân viên làm theo ca."];
        CheckpointCriterionCues.Find("Target Customer Definition", Target.Excellent, real,
            real.Select(line => MentorEvidenceText.Normalize(line)).ToArray()).Single().Found.Should().BeTrue();
    }

    [Fact]
    public void Find_DoesNotQuoteHowTheSurveyWasDoneAsTheMarketProblem()
    {
        const string deck =
            "Thang đo chuẩn hóa: Sử dụng thang đo Likert 5 mức độ để đánh giá mức độ hài lòng, tần suất rủi ro và mức độ cần thiết.\n" +
            "Đối mặt rủi ro cao nhất về thiếu sót chứng từ khi Hải quan đối soát.";

        CheckpointSummaryEvidence.Find(deck, [ProblemAnalysis], CheckpointSummaryTopics.None)[0].Evidence
            .Should().StartWith("Đối mặt rủi ro cao nhất");
    }

    [Fact]
    public void Find_StillQuotesMethodologyForACriterionAboutResearch()
    {
        var research = Criterion(0, "Market Research Quality", "Research uses multiple credible methods and sources");
        const string deck =
            "Phương pháp thu thập dữ liệu: khảo sát bảng hỏi trực tiếp, mẫu 51 doanh nghiệp.\n" +
            "Đối mặt rủi ro cao nhất về thiếu sót chứng từ khi Hải quan đối soát.";

        CheckpointSummaryEvidence.Find(deck, [research], CheckpointSummaryTopics.None)[0].Evidence
            .Should().StartWith("Phương pháp thu thập dữ liệu");
    }

    [Theory]
    [InlineData("Thang đo chuẩn hóa: Sử dụng thang đo Likert 5 mức độ.", true)]
    [InlineData("Thời gian thực hiện: Từ 24/09/2026 đến 04/10/2026.", true)]
    [InlineData("Quy mô mẫu hợp lệ: N = 51 nhân sự.", true)]
    [InlineData("84.3% nhân sự tốn hơn 10 phút chỉ để tìm lại một bộ chứng từ cũ.", false)]
    public void IsMethodology_RecognisesLinesThatDescribeTheMethod(string line, bool expected)
    {
        CheckpointSummaryText.IsMethodology(line).Should().Be(expected);
    }

    [Fact]
    public void Highlights_SkipMethodLines()
    {
        const string text =
            "Thời gian thực hiện: Từ 24/09/2026 đến 04/10/2026 tại các doanh nghiệp logistics.\n" +
            "Thang đo chuẩn hóa: Sử dụng thang đo Likert 5 mức độ để đánh giá mức độ hài lòng của khách hàng.\n" +
            "Lãng phí vô hình: 84,3% nhân sự tốn hơn 10 phút chỉ để tìm lại một bộ chứng từ cũ ở khách hàng.";

        CheckpointSummaryText.Highlights(text, ["Market problem"], 1).Should().ContainSingle().Which.Should().StartWith("Lãng phí vô hình");
    }
}
