using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

/// <summary>Problems found on real submissions of EXE101 Checkpoint 2 (survey, market report, slide decks in Vietnamese).</summary>
public sealed class CheckpointProductionFindingsTests
{
    private static ChecklistItem Criterion(int index, string name, string excellent, decimal weight = 20) =>
        new(index, "Criterion", name, excellent, weight, CheckpointCriterionProfile.Classify(name), excellent);

    [Fact]
    public void SummaryLimit_CoversEveryFileThatCanBeUploaded()
    {
        // A 62 MB slide deck was never summarised because the limit was 30 MB; it reads in half a second.
        CheckpointSummaryLimits.MaxFileSizeBytes.Should().BeGreaterThanOrEqualTo(SubmissionFileLimits.MaxFileSizeBytes);
        CheckpointSummaryLimits.MaxFileSizeBytes.Should().BeGreaterThan(62L * 1024 * 1024);
    }

    [Fact]
    public void StatusLines_DoNotReadVietnameseProseAsAStatusTableRow()
    {
        // "Đo lường hiệu quả thay vì chỉ hoàn thành khóa học" is a sentence, not "item — Done".
        CheckpointStatusLines.Find("Đo lường hiệu quả thay vì chỉ hoàn thành khóa học").Should().BeEmpty();
        CheckpointStatusLines.Find("Market Research Completed Problem validated").Should().ContainSingle().Which.Status.Should().Be("Done");
    }

    [Fact]
    public void Cues_RecogniseVietnameseTargetCustomerAndSurveyResearch()
    {
        string[] file =
        [
            "Phân khúc ban đầu: F&B nhiều chi nhánh, nhân viên làm theo ca.",
            "100+ người trả lời khảo sát, 83% là người trực tiếp quản lý nhân viên."
        ];
        var normalised = file.Select(line => MentorRankingFeatures.Normalize(line)).ToArray();

        var target = CheckpointCriterionCues.Find("Target Customer Definition",
            "Target customer is specific, well-segmented, and supported by data", file, normalised);
        var research = CheckpointCriterionCues.Find("Market Research Quality",
            "Research uses multiple credible methods and sources", file, normalised);

        target.Should().ContainSingle(cue => cue.Label == "Target customer").Which.Found.Should().BeTrue();
        research.Should().ContainSingle(cue => cue.Label == "Research").Which.Found.Should().BeTrue();
        // A persona is its own thing: a segment is not a persona.
        CheckpointCriterionCues.Wanted("Persona & CJM", null).Should().Contain("Persona").And.NotContain("Target customer");
    }

    [Fact]
    public void Find_DoesNotQuoteOneSentenceForEveryCriterionWhenAnotherFitsBetter()
    {
        ChecklistItem[] rubric =
        [
            Criterion(0, "Market Problem Analysis", "Market problem is analyzed deeply with credible evidence"),
            Criterion(1, "Market Research Quality", "Research uses multiple credible methods and sources"),
            Criterion(2, "Problem–Solution Fit", "Strong, well-argued alignment between problem and proposed solution")
        ];
        const string survey =
            "Dữ liệu nghiên cứu từ 52 doanh nghiệp | Phân tích thực trạng và đề xuất giải pháp số hóa\n" +
            "Phương pháp khảo sát bảng hỏi trực tiếp, mẫu 52 doanh nghiệp, thang đo Likert 5 mức độ.\n" +
            "Vấn đề lớn nhất: 82,7% doanh nghiệp mất hơn 11 phút để tìm lại chứng từ cũ.\n" +
            "Giải pháp đề xuất: tìm kiếm OCR và cảnh báo chứng từ hết hạn tự động.";

        var findings = CheckpointSummaryEvidence.Find(survey, rubric, CheckpointSummaryTopics.None);

        findings.Select(item => item.Evidence).Distinct().Should().HaveCount(3, "each criterion quotes the sentence most about it");
        findings[1].Evidence.Should().Contain("khảo sát");
    }

    [Fact]
    public void Highlights_SkipEmojiBannersAndDashboardInstructions()
    {
        const string text =
            "🎯 CHỌN LOẠI HÌNH DOANH NGHIỆP 💡 Hướng dẫn tương tác: chọn loại hình tại menu thả xuống bên trái để xem phân tích riêng.\n" +
            "82,7% doanh nghiệp mất từ 11 đến hơn 30 phút để tìm bộ chứng từ cũ, rủi ro lớn nhất là thiếu sót khi kiểm tra Hải quan.\n" +
            "98% doanh nghiệp sẵn sàng trải nghiệm bản dùng thử sớm với ngân sách 2,5 đến 5 triệu mỗi tháng.";

        var highlights = CheckpointSummaryText.Highlights(text, ["Market research"], 2);

        highlights.Should().NotContain(item => item.Contains("Hướng dẫn tương tác"));
        highlights.Should().HaveCount(2);
    }

    [Fact]
    public void Combine_QuotesTheFileThatMentionsMostOfWhatTheCriterionExpects()
    {
        var checklist = new[] { Criterion(0, "Market Problem Analysis", "Market problem is analyzed deeply with credible evidence") };
        WorkspaceFileRequirementFinding Finding(string quote, params bool[] found) => new()
        {
            Index = 0, Kind = "Criterion", Title = "Market Problem Analysis", Finding = "Related", Evidence = quote,
            Cues = found.Select((value, index) => new WorkspaceCriterionCue { Label = $"cue{index}", Found = value }).ToArray()
        };

        // The newest file mentions one expected thing, the older one mentions two.
        var combined = CheckpointSummaryOutput.Combine(checklist,
        [
            ("new.pdf", [Finding("Thin quote", true, false)]),
            ("old.pdf", [Finding("Rich quote", true, true)])
        ]);

        combined[0].Evidence.Should().Be("Rich quote");
        combined[0].Source.Should().Be("old.pdf");
    }
}
