using EHub.Application.Features.Workspaces.CheckpointSummaries;
using EHub.Contracts.Workspaces;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointSummaryReviewTests
{
    private static readonly ChecklistItem[] Rubric =
    [
        new(0, "Criterion", "Problem Clarity", "Clearly defined problem statement, customer pain points, and target persona."),
        new(1, "Criterion", "Business Model", "Revenue model, pricing strategy, and initial cost assumptions are logical.")
    ];

    [Fact]
    public void Prompt_ShowsTheChecklistOnlyWhenTheFileFollowsIt()
    {
        var pitch = CheckpointSummaryPrompt.User(1, "Idea", Rubric, "deck.pdf", "Nội dung", false, followChecklist: true);
        var review = CheckpointSummaryPrompt.User(1, "Idea", Rubric, "review.pdf", "Nội dung", false, followChecklist: false, hasStatus: true);

        CheckpointSummaryPrompt.Version.Should().Be("v10");
        pitch.Should().Contain("- Problem Clarity: Clearly defined").And.Contain("mỗi ý về một mục khác nhau trong danh sách cần xem");
        // A file that does not cover the rubric is summarised on its own terms, without the labels to fill.
        review.Should().NotContain("Problem Clarity").And.Contain("không gắn nhãn")
            .And.Contain("ghi rõ trạng thái");
    }

    [Fact]
    public void StatusLines_FindLabelledStatusesAndTableRows()
    {
        const string text =
            "Mục 3.2 của Report 3 không đúng template.\n" +
            "DONE\n" +
            "NHÓM ĐÃ TIẾP THU VÀ CẬP NHẬT REPORT 3 THEO ĐẦY ĐỦ GÓP Ý.\n" +
            "1. PENDING: NHÓM SẼ NGHIÊN CỨU SAU KHI HOÀN THÀNH PHẠM VI.\n" +
            "2. IN PROGRESS: SỬ DỤNG AI CẢNH BÁO TRÙNG ĐỀ TÀI.\n" +
            "Beta Launch In Progress Public beta preparation\n" +
            "Market Research Completed Problem validated\n" +
            "We are done with the interviews and the analysis of all forty answers from students in three classes.";

        var items = CheckpointStatusLines.Find(text);

        items.Select(item => item.Status).Should().Equal("Done", "Pending", "InProgress", "InProgress", "Done");
        items[0].Text.Should().StartWith("Nhóm đã tiếp thu");
        items[1].Text.Should().StartWith("Nhóm sẽ nghiên cứu");
        items[3].Text.Should().Be("Beta Launch — Public beta preparation");
        // A long sentence that happens to say "done" is prose, not a status.
        items.Should().NotContain(item => item.Text.Contains("interviews"));
    }

    [Fact]
    public void Grounding_RejectsAMarketSizePresentedAsATarget()
    {
        var grounding = new CheckpointSummaryGrounding(
            "Estimated at 2 million university students and academic users\n" +
            "2-Year Capture Target: 4-5,000 Active Users.\n" +
            "Reach 2,000 Registered Users in Q4/2026.");

        grounding.MisusesMarketSize("Thị trường: Mục tiêu là thu hút 2 triệu sinh viên trong năm đầu tiên.").Should().BeTrue();
        grounding.Supports("Thị trường: Mục tiêu là thu hút 2 triệu sinh viên trong năm đầu tiên.").Should().BeFalse();
        // Stating the market size as a market size is fine.
        grounding.MisusesMarketSize("Thị trường ước tính 2 triệu sinh viên và người dùng học thuật.").Should().BeFalse();
    }

    [Fact]
    public void Grounding_IgnoresTheLabelInFrontOfAKeyPoint()
    {
        var grounding = new CheckpointSummaryGrounding("Sinh viên mất nhiều thời gian sửa lỗi định dạng khi copy từ ChatGPT sang Word.");

        grounding.Supports("Vấn đề: sinh viên mất nhiều thời gian sửa lỗi định dạng khi copy sang Word.").Should().BeTrue();
    }

    [Fact]
    public void TryBuild_DropsQuestionsForImageFilesAndQuestionsBuiltOnAMisreadFigure()
    {
        const string json = """{"overview":"Kế hoạch khởi nghiệp.","keyPoints":[],"questions":["Làm sao thu hút 2 triệu sinh viên trong năm đầu?","Nhóm định giá thế nào?"]}""";
        var grounding = new CheckpointSummaryGrounding("Estimated at 2 million university students.\nPricing 39K VND per month.");

        CheckpointSummaryOutput.TryBuild(json, new WorkspaceFilePreview(), grounding)!.Questions
            .Should().Equal("Nhóm định giá thế nào?");
        CheckpointSummaryOutput.TryBuild(json, new WorkspaceFilePreview { LittleText = true }, grounding)!.Questions
            .Should().BeEmpty();
    }

    [Fact]
    public void Evidence_AddsTheFollowingLinesToAShortQuote()
    {
        const string deck =
            "KEY CUSTOMER PAIN\n" +
            "TARGET AUDIENCE\n" +
            "Students & Working Professionals\n" +
            "Frequently copy content from AI, websites and PDFs into Word\n" +
            "Perform repetitive copy and formatting tasks\n" +
            "REVENUE MODEL\n";

        var finding = CheckpointSummaryEvidence.Find(deck, Rubric, CheckpointSummaryTopics.None)[0];

        finding.Evidence.Should().Be(
            "Students & Working Professionals … Frequently copy content from AI, websites and PDFs into Word … Perform repetitive copy and formatting tasks");
    }
}
