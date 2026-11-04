using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

/// <summary>Problems found in the summary of a survey slide deck (SLIDE TEAM 3.pdf, EXE101 Checkpoint 2).</summary>
public sealed class CheckpointSurveyDeckFindingsTests
{
    private const string Deck =
        "Table of Contents\n" +
        "Thực hiện khảo sát thị trường Đối thủ cạnh tranh\n" +
        "Phỏng vấn chuyên sâu Phân khúc Khách hàng\n" +
        "Tổng quan thị trường, Quy mô & Xu hướng\n" +
        "Chi phí & Giá của sản phẩm\n" +
        "Thời gian thực hiện: Từ 24/09/2026 đến 04/10/2026.\n" +
        "Quy mô mẫu hợp lệ: N = 51 nhân sự hoạt động trong lĩnh vực Logistics.\n" +
        "Cơ cấu doanh nghiệp tham gia khảo sát: FreightForwarder chiếm 35,3% (18 doanh nghiệp), 3PL chiếm 33,7%.\n" +
        "2. Nếu không quản lý tốt chứng từ, rủi ro / chi phí nào có thể phát sinh?\n" +
        "Nhập liệu thủ công tốn 15–20 phút/bộ chứng từ, tạo ra gánh nặng chi phí.\n" +
        "Phương pháp phân tích: Thống kê định lượng cốt lõi gồm Min, Max, Trung vị, Trung bình và Yếu vị.\n" +
        "62,7% có ngân sách từ 2,5 triệu trở lên\n" +
        "Ngân sách dự kiến: 3.000 USD/năm/doanh nghiệp.";

    private static readonly ChecklistItem[] Rubric =
    [
        new(0, "Criterion", "Target Customer Definition", "Target customer is specific, well-segmented, and supported by data", 20, "Document",
            "Target customer is specific, well-segmented, and supported by data"),
        new(1, "Criterion", "Market Problem Analysis", "Market problem is analyzed deeply with credible evidence", 20, "Document",
            "Market problem is analyzed deeply with credible evidence")
    ];

    [Fact]
    public void Grounding_RejectsAnAmountInDollarsThatTheFileGivesWithoutACurrency()
    {
        var grounding = new CheckpointSummaryGrounding(Deck);

        // "2,5 triệu" in the file; the summary said "2.5 triệu USD".
        grounding.InventsCurrency("Khách hàng sẵn sàng chi trả, với 62.7% có ngân sách từ 2.5 triệu USD trở lên.").Should().BeTrue();
        grounding.Supports("Khách hàng sẵn sàng chi trả, với 62.7% có ngân sách từ 2.5 triệu USD trở lên.").Should().BeFalse();
        // Correct, or only naming dong, or a dollar amount the file really gives in dollars.
        grounding.InventsCurrency("62,7% có ngân sách từ 2,5 triệu trở lên.").Should().BeFalse();
        grounding.InventsCurrency("Ngân sách từ 2,5 triệu đồng trở lên.").Should().BeFalse();
        grounding.InventsCurrency("Ngân sách dự kiến là 3.000 USD mỗi năm cho một doanh nghiệp.").Should().BeFalse();
    }

    [Fact]
    public void ContentsLines_AreTheAgendaAfterTheContentsHeadingUpToTheFirstSentence()
    {
        var contents = CheckpointSummaryText.ContentsLines(CheckpointSummaryText.Sentences(Deck));

        contents.Take(6).Should().Equal(true, true, true, true, true, false);
        contents.Skip(5).Should().OnlyContain(item => !item);
    }

    [Fact]
    public void Find_DoesNotQuoteAnAgendaItemOrTheAnalysisMethodAsEvidence()
    {
        var findings = CheckpointSummaryEvidence.Find(Deck, Rubric, CheckpointSummaryTopics.None);

        // The agenda says "Phân khúc Khách hàng" but shows nothing about the segments: no evidence is better than the agenda.
        findings[0].Evidence.Should().NotContain("Phỏng vấn chuyên sâu");
        // How the data is analysed is not what the problem is.
        findings[1].Cues.Where(cue => cue.Label == "Insights / lessons").Should().BeEmpty();
        findings[1].Evidence.Should().NotContain("Phương pháp phân tích");
    }

    [Fact]
    public void Find_PrefersAFindingOverASurveyQuestionAsEvidence()
    {
        const string text =
            "Mức độ quan trọng của quản lý chứng từ hiệu quả và hạn chế các rủi ro thất lạc?\n" +
            "Rủi ro lớn nhất: 82,7% doanh nghiệp mất hơn 11 phút để tìm lại chứng từ cũ khi kiểm tra Hải quan.";
        ChecklistItem[] rubric =
        [
            new(0, "Criterion", "Market Problem Analysis", "Market problem is analyzed deeply with credible evidence", 20, "Document",
                "Market problem is analyzed deeply with credible evidence")
        ];

        CheckpointSummaryEvidence.Find(text, rubric, CheckpointSummaryTopics.None)[0].Evidence.Should().StartWith("Rủi ro lớn nhất");
    }

    [Fact]
    public void Highlights_SkipSurveyQuestionsAndTheAgenda()
    {
        var highlights = CheckpointSummaryText.Highlights(Deck, ["Market research"], 3);

        highlights.Should().NotContain(item => item.EndsWith('?'));
        highlights.Should().NotContain(item => item.Contains("Phỏng vấn chuyên sâu"));
        highlights.Should().Contain("Nhập liệu thủ công tốn 15–20 phút/bộ chứng từ, tạo ra gánh nặng chi phí.");
    }
}
